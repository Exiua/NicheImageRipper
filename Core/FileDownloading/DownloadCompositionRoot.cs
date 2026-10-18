using NicheImageRipper.Core.FileDownloading.FileDownloadStrategies;
using NicheImageRipper.Core.ModuleLoading;
using NicheImageRipper.Core.Utility;
using NicheImageRipper.Sdk.FileDownloading;

namespace NicheImageRipper.Core.FileDownloading;

internal static class DownloadCompositionRoot
{
    public static FileDownloadStrategyRegistry BuildStrategyRegistry()
    {
        // Strategies live in the site modules, which have to be loaded before they can be discovered
        CoreBootstrap.EnsureInitialized();
        var strategyTypes = AppDomain.CurrentDomain
                                     .GetAssemblies()
                                     .SelectMany(a =>
                                      {
                                          try
                                          {
                                              return a.GetTypes();
                                          }
                                          catch
                                          {
                                              return [];
                                          }
                                      })
                                     .Where(t => !t.IsAbstract && typeof(IFileDownloadStrategy).IsAssignableFrom(t));

        var strategies = strategyTypes.Select(t => (IFileDownloadStrategy)Activator.CreateInstance(t)!);
        return new FileDownloadStrategyRegistry(strategies);
    }

    public static IReadOnlyList<IRequestHeaderModifier> BuildHeaderModifiers() =>
        ReflectionDiscovery.DiscoverImplementations<IRequestHeaderModifier>();

    public static IReadOnlyList<IDownloadErrorHandler> BuildErrorHandlers() =>
        ReflectionDiscovery.DiscoverImplementations<IDownloadErrorHandler>();

    public static IReadOnlyList<IPostDownloadValidator> BuildPostDownloadValidators() =>
        ReflectionDiscovery.DiscoverImplementations<IPostDownloadValidator>();

    public static IReadOnlyList<IExternalToolDownloadStrategy> BuildExternalToolStrategies() =>
        ReflectionDiscovery.DiscoverImplementations<IExternalToolDownloadStrategy>();

    public static IReadOnlyList<IWebDriverPreferenceProvider> BuildWebDriverPreferenceProviders() =>
        ReflectionDiscovery.DiscoverImplementations<IWebDriverPreferenceProvider>();
}