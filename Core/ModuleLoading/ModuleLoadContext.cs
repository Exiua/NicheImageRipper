using System.Reflection;
using System.Runtime.Loader;

namespace NicheImageRipper.Core.ModuleLoading;

internal sealed class ModuleLoadContext(string mainAssemblyPath) : AssemblyLoadContext(isCollectible: false)
{
    private static readonly HashSet<string> SharedAssemblyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "NicheImageRipper.Sdk",
        "HtmlAgilityPack",
        "Selenium.WebDriver",
        "Serilog",
        "FlareSolverrIntegration",
        "CSWebDriverClient",
    };

    private readonly AssemblyDependencyResolver _resolver = new(mainAssemblyPath);

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (assemblyName.Name is not null && SharedAssemblyNames.Contains(assemblyName.Name))
        {
            return null;
        }

        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is not null ? LoadFromAssemblyPath(path) : null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is not null ? LoadUnmanagedDllFromPath(path) : IntPtr.Zero;
    }
}