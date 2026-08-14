using System.Reflection;
using System.Runtime.Loader;
using Serilog;

namespace NicheImageRipper.Core.Utility;

/// <summary>
///     Discovers and loads site-provider assemblies: the compiled-in SiteModules assembly, plus any
///     third-party modules dropped into subfolders of modules/ next to the executable. Load order is
///     alphanumeric by module folder name, unless a loadOrder.json file lists an explicit order — later
///     entries load last and take precedence on any name collision (see HtmlParserFactory.BuildSupportedUrls
///     -style "last wins" merge behavior).
/// </summary>
public static class SiteModuleLoader
{
    private const string BuiltInModuleAssemblyName = "NicheImageRipper.SiteModules";
    private const string ModulesFolderName = "modules";
    private const string LoadOrderFileName = "loadOrder.json";

    private static readonly ILogger Logger = Log.ForContext(typeof(SiteModuleLoader));

    /// <summary>Loads all site-provider modules. Safe to call more than once — module folders already loaded
    /// are skipped on subsequent calls (assembly loading is inherently idempotent per-context).</summary>
    public static IReadOnlyList<Assembly> LoadModules()
    {
        var loaded = new List<Assembly>();

        var builtIn = LoadBuiltInModule();
        if (builtIn is not null)
        {
            loaded.Add(builtIn);
        }

        loaded.AddRange(LoadDroppedInModules());
        return loaded;
    }

    private static Assembly? LoadBuiltInModule()
    {
        try
        {
            return Assembly.Load(BuiltInModuleAssemblyName);
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to load built-in module assembly {AssemblyName}", BuiltInModuleAssemblyName);
            return null;
        }
    }

    private static IEnumerable<Assembly> LoadDroppedInModules()
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
                continue;
            }

            var assembly = LoadModuleFolder(folderPath);
            if (assembly is not null)
            {
                yield return assembly;
            }
        }
    }

    /// <summary>
    ///     Determines module load order: an explicit loadOrder.json array if present (any folders it omits
    ///     are appended afterward, alphanumerically), otherwise plain alphanumeric folder-name order.
    /// </summary>
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

    private static Assembly? LoadModuleFolder(string folderPath)
    {
        var mainDll = Directory.GetFiles(folderPath, "*.dll")
                               .FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Equals(Path.GetFileName(folderPath), StringComparison.OrdinalIgnoreCase))
                      ?? Directory.GetFiles(folderPath, "*.dll").FirstOrDefault();

        if (mainDll is null)
        {
            Logger.Warning("Module folder {FolderPath} contains no .dll files, skipping", folderPath);
            return null;
        }

        try
        {
            var context = new ModuleLoadContext(mainDll);
            var assembly = context.LoadFromAssemblyPath(mainDll);
            Logger.Information("Loaded module: {AssemblyName} from {FolderPath}", assembly.GetName().Name, folderPath);
            return assembly;
        }
        catch (Exception e)
        {
            Logger.Warning(e, "Failed to load module from {FolderPath}, skipping", folderPath);
            return null;
        }
    }

    private sealed class ModuleLoadContext(string mainAssemblyPath) : AssemblyLoadContext(isCollectible: false)
    {
        private readonly AssemblyDependencyResolver _resolver = new(mainAssemblyPath);

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            var path = _resolver.ResolveAssemblyToPath(assemblyName);
            return path is not null ? LoadFromAssemblyPath(path) : null;
        }
    }
}