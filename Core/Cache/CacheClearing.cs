using NicheImageRipper.Sdk.Cache;
using Serilog;

namespace NicheImageRipper.Core.Cache;

/// <summary>
/// Discovers every loaded ICacheOwner implementer and clears its cache. Scans all loaded types (not
/// just HtmlParser-derived ones, since cache ownership isn't limited to parsers — Core's own managers
/// like PartialSaveManager implement this too), so a new cache file only needs the interface added to
/// its owning type, never a change here or in NicheImageRipper.ClearCache.
/// </summary>
public class CacheClearing
{
    private static readonly ILogger Logger = Log.ForContext(typeof(CacheClearing));

    public static void ClearAll()
    {
        var owners = AppDomain.CurrentDomain
                              .GetAssemblies()
                              .SelectMany(assembly =>
                               {
                                   try
                                   {
                                       return assembly.GetTypes();
                                   }
                                   catch
                                   {
                                       return [];
                                   }
                               })
                              .Where(type => !type.IsAbstract && typeof(ICacheOwner).IsAssignableFrom(type));

        foreach (var type in owners)
        {
            try
            {
                var method = type.GetMethod(nameof(ICacheOwner.ClearCache),
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!;
                method.Invoke(null, null);
                Logger.Debug("Cleared cache for {Type}", type.Name);
            }
            catch (Exception e)
            {
                Logger.Warning(e, "Failed to clear cache for {Type}", type.Name);
            }
        }
    }
}