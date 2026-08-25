using System.Runtime.CompilerServices;
using Sdk.DataStructures;

namespace NicheImageRipper.Core.SiteParsing;

internal static class LinkInfoDiscovery
{
    /// <summary>
    ///     Forces the static constructor of every loaded ILinkInfoProvider to run, registering its LinkInfo
    ///     values before anything (e.g. PartialSaveManager deserializing a cached FileLink) might need to
    ///     resolve them by name. Call once during startup, after SiteModuleLoader.LoadModules().
    /// </summary>
    public static void EnsureAllRegistered()
    {
        var providerTypes = AppDomain.CurrentDomain
                                     .GetAssemblies()
                                     .SelectMany(a =>
                                      {
                                          try { return a.GetTypes(); }
                                          catch { return []; }
                                      })
                                     .Where(t => typeof(ILinkInfoProvider).IsAssignableFrom(t) && !t.IsAbstract);

        foreach (var type in providerTypes)
        {
            RuntimeHelpers.RunClassConstructor(type.TypeHandle);
        }
    }
}