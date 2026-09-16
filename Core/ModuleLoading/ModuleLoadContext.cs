using System.Reflection;
using System.Runtime.Loader;

namespace NicheImageRipper.Core.ModuleLoading;

internal sealed class ModuleLoadContext : AssemblyLoadContext
{
    // Anything whose types can flow across the Sdk boundary must be shared — a plugin loading its
    // own copy would produce types that fail identity checks (is/as/pattern-match) against Core's
    // or another plugin's copy of the "same" type. Not just Sdk itself: every assembly whose types
    // appear in Sdk's public API surface (HtmlNode from Soupify, WebDriver/Selenium types, ILogger).
    private static readonly HashSet<string> SharedAssemblyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "NicheImageRipper.Sdk",
        "HtmlAgilityPack",
        "Selenium.WebDriver",
        "Serilog",
        "FlareSolverrIntegration",
        "CSWebDriverClient",
    };

    private readonly AssemblyDependencyResolver _resolver;

    public ModuleLoadContext(string mainAssemblyPath) : base(isCollectible: false)
    {
        _resolver = new AssemblyDependencyResolver(mainAssemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (assemblyName.Name is not null && SharedAssemblyNames.Contains(assemblyName.Name))
        {
            // Defer to Default - returning null tells the runtime to fall back and resolve there.
            return null;
        }

        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is not null ? LoadFromAssemblyPath(path) : null; // null → Default as last resort
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is not null ? LoadUnmanagedDllFromPath(path) : IntPtr.Zero;
    }
}