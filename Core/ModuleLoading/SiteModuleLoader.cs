using System.Reflection;
using System.Runtime.Loader;
using NicheImageRipper.Sdk.Utility;
using Serilog;

namespace NicheImageRipper.Core.ModuleLoading;

public enum ModuleLoadStatus
{
    Loaded,
    Failed,   // found, but threw while loading
    Skipped,  // found, but nothing loadable (e.g. no .dll)
    Missing   // listed in loadOrder.json, but no matching folder
}

public sealed record ModuleLoadResult(
    string Name,
    ModuleLoadStatus Status,
    string? Path = null,
    Assembly? Assembly = null,
    string? Reason = null,
    Exception? Exception = null,
    bool IsBuiltIn = false)
{
    public bool Succeeded => Status == ModuleLoadStatus.Loaded;
}

public static class SiteModuleLoader
{
    private const string BuiltInModuleAssemblyName = "SiteModules.dll";
    private const string ModulesFolderName = "modules";
    private const string LoadOrderFileName = "loadOrder.json";

    private static readonly ILogger Logger = Log.ForContext(typeof(SiteModuleLoader));

    /// <summary>
    ///     Attempts to load all site-provider modules and returns one result per module attempted,
    ///     in load order, whether it loaded successfully.
    /// </summary>
    public static IReadOnlyList<ModuleLoadResult> LoadModules()
    {
        var results = new List<ModuleLoadResult>();

        Logger.Debug("Loading built-in module assembly {AssemblyName} from {BaseDirectory}", BuiltInModuleAssemblyName, AppContext.BaseDirectory);
        results.Add(LoadBuiltInModule());
        results.AddRange(LoadDroppedInModules());

        Logger.Information("Module load summary: {Loaded} loaded, {Failed} failed, {Other} skipped/missing",
            results.Count(r => r.Status == ModuleLoadStatus.Loaded),
            results.Count(r => r.Status == ModuleLoadStatus.Failed),
            results.Count(r => r.Status is ModuleLoadStatus.Skipped or ModuleLoadStatus.Missing));

        return results;
    }

    /// <summary>
    ///     Convenience for callers that only want the assemblies that loaded.
    /// </summary>
    public static IEnumerable<Assembly> GetLoadedAssemblies(this IEnumerable<ModuleLoadResult> results) =>
        results.Where(r => r.Assembly is not null).Select(r => r.Assembly!);

    private static ModuleLoadResult LoadBuiltInModule()
    {
        var path = Path.Combine(AppContext.BaseDirectory, BuiltInModuleAssemblyName);
        try
        {
            var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
            Logger.Debug("Loaded built-in module assembly {AssemblyName}", BuiltInModuleAssemblyName);
            return new ModuleLoadResult(BuiltInModuleAssemblyName, ModuleLoadStatus.Loaded, path, assembly, IsBuiltIn: true);
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to load built-in module assembly {AssemblyName}", BuiltInModuleAssemblyName);
            return new ModuleLoadResult(BuiltInModuleAssemblyName, ModuleLoadStatus.Failed, path,
                Reason: e.Message, Exception: e, IsBuiltIn: true);
        }
    }

    private static IEnumerable<ModuleLoadResult> LoadDroppedInModules()
    {
        var modulesPath = Path.Combine(AppContext.BaseDirectory, ModulesFolderName);
        if (!Directory.Exists(modulesPath))
        {
            Logger.Debug("Modules folder not found at {ModulesPath}, skipping drop-in module loading", modulesPath);
            yield break;
        }

        var moduleFolders = Directory.GetDirectories(modulesPath)
                                     .ToDictionary(Path.GetFileName, d => d, StringComparer.OrdinalIgnoreCase)!;

        foreach (var folderName in ResolveLoadOrder(modulesPath, moduleFolders.Keys))
        {
            if (!moduleFolders.TryGetValue(folderName, out var folderPath))
            {
                Logger.Warning("loadOrder.json lists module '{Module}' but no matching folder was found under {ModulesPath}",
                    folderName, modulesPath);
                yield return new ModuleLoadResult(folderName, ModuleLoadStatus.Missing,
                    Reason: "Listed in loadOrder.json but no matching folder was found");
                continue;
            }

            yield return LoadModuleFolder(folderName, folderPath);
        }
    }

    private static IEnumerable<string> ResolveLoadOrder(string modulesPath, ICollection<string> discoveredFolders)
    {
        var loadOrderPath = Path.Combine(modulesPath, LoadOrderFileName);
        if (!File.Exists(loadOrderPath))
        {
            return discoveredFolders.OrderBy(f => f, StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var explicitOrder = JsonUtility.Deserialize<List<string>>(loadOrderPath) ?? [];
            var remaining = discoveredFolders
                           .Except(explicitOrder, StringComparer.OrdinalIgnoreCase)
                           .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);
            return explicitOrder.Concat(remaining);
        }
        catch (Exception e)
        {
            Logger.Warning(e, "Failed to read {LoadOrderFile}, falling back to alphanumeric order", loadOrderPath);
            return discoveredFolders.OrderBy(f => f, StringComparer.OrdinalIgnoreCase);
        }
    }

    private static ModuleLoadResult LoadModuleFolder(string folderName, string folderPath)
    {
        var dlls = Directory.GetFiles(folderPath, "*.dll");
        var mainDll = dlls.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f)
                                                   .Equals(folderName, StringComparison.OrdinalIgnoreCase))
                      ?? dlls.FirstOrDefault();

        if (mainDll is null)
        {
            Logger.Warning("Module folder {FolderPath} contains no .dll files, skipping", folderPath);
            return new ModuleLoadResult(folderName, ModuleLoadStatus.Skipped, folderPath,
                Reason: "Folder contains no .dll files");
        }

        try
        {
            var context = new ModuleLoadContext(mainDll);
            var assembly = context.LoadFromAssemblyPath(mainDll);
            Logger.Information("Loaded module: {AssemblyName} from {FolderPath}", assembly.GetName().Name, folderPath);
            return new ModuleLoadResult(folderName, ModuleLoadStatus.Loaded, mainDll, assembly);
        }
        catch (Exception e)
        {
            Logger.Warning(e, "Failed to load module from {FolderPath}, skipping", folderPath);
            return new ModuleLoadResult(folderName, ModuleLoadStatus.Failed, mainDll,
                Reason: e.Message, Exception: e);
        }
    }
}