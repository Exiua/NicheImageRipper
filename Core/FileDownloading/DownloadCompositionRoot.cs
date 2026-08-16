using NicheImageRipper.Core.FileDownloading.FileDownloadStrategies;
using NicheImageRipper.Core.Utility;

namespace NicheImageRipper.Core.FileDownloading;

internal static class DownloadCompositionRoot
{
    public static FileDownloadStrategyRegistry BuildStrategyRegistry() => new(
    [
        new TextDownloadStrategy(),
        new Base64DownloadStrategy(),
        //new MegaDownloadStrategy(),
        new GenericHttpDownloadStrategy(),
        //new GDriveDownloadStrategy(),
        new IframeMediaDownloadStrategy(),
        //new PixelDrainDownloadStrategy(),
        //new YoutubeVideoDownloadStrategy(),
        new MpegDashDownloadStrategy(),
        new ResolveImageDownloadStrategy(),
        new SeleniumImageDownloadStrategy(),
        //new PixivUgoiraDownloadStrategy(),
        //new SteamCommunityDownloadStrategy(),
        //new IwaraDownloadStrategy(),
        new M3U8FfmpegDownloadStrategy(),
        new M3U8YtDlpDownloadStrategy(),
        new ObfuscatedM3U8DownloadStrategy(),
    ]);

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