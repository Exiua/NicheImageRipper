using NicheImageRipper.Core.SiteParsing;
using NicheImageRipper.Sdk.SiteParsing;

namespace NicheImageRipper.Core.ModuleLoading;

/// <summary>
///     One-time startup for anything that discovers implementations by scanning loaded assemblies: site modules
///     have to be loaded first, or their parsers, strategies, link infos and cache owners are silently missed.
///     Safe to call from anywhere, any number of times.
/// </summary>
internal static class CoreBootstrap
{
    private static readonly Lazy<bool> Initialized = new(Initialize, LazyThreadSafetyMode.ExecutionAndPublication);

    public static void EnsureInitialized() => _ = Initialized.Value;

    private static bool Initialize()
    {
        // Must not touch HtmlParserFactory: its static constructor calls EnsureInitialized
        SiteModuleLoader.LoadModules();
        LinkInfoDiscovery.EnsureAllRegistered();
        SiteLinkRuleRegistry.Refresh(); // pick up the rules in the modules just loaded
        UserAgentRuleRegistry.Refresh();
        HtmlParser.ParserResolver = new CoreParserResolver();
        return true;
    }
}
