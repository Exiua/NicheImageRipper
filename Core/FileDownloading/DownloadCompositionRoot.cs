using NicheImageRipper.Core.FileDownloading.DownloadErrorHandlers;
using NicheImageRipper.Core.FileDownloading.FileDownloadStrategies;
using NicheImageRipper.Core.FileDownloading.PostDownloadValidators;
using NicheImageRipper.Core.FileDownloading.RequestHeaderModifier;
using NicheImageRipper.Core.Utility;

namespace NicheImageRipper.Core.FileDownloading;

internal static class DownloadCompositionRoot
{
    public static FileDownloadStrategyRegistry BuildStrategyRegistry() => new(
    [
        new TextDownloadStrategy(),
        new Base64DownloadStrategy(),
        new MegaDownloadStrategy(),
        new GenericHttpDownloadStrategy(),
        new GDriveDownloadStrategy(),
        new IframeMediaDownloadStrategy(),
        new PixelDrainDownloadStrategy(),
        new YoutubeVideoDownloadStrategy(),
        new MpegDashDownloadStrategy(),
        new ResolveImageDownloadStrategy(),
        new SeleniumImageDownloadStrategy(),
        new PixivUgoiraDownloadStrategy(),
        new SteamCommunityDownloadStrategy(),
        new IwaraDownloadStrategy(),
        new M3U8FfmpegDownloadStrategy(),
        new M3U8YtDlpDownloadStrategy(),
        new ObfuscatedM3U8DownloadStrategy(),
    ]);

    public static IReadOnlyList<IRequestHeaderModifier> BuildHeaderModifiers() =>
    [
        new RedgifsAuthHeaderModifier(),
        new GoFileCookieHeaderModifier(),
        new DonmaiUserAgentHeaderModifier(),
    ];

    public static IReadOnlyList<IDownloadErrorHandler> BuildErrorHandlers() =>
    [
        new PixivExtensionFallbackErrorHandler(),
        new KemonoBadSubdomainErrorHandler(),
        new EHentaiExpiredLinkErrorHandler(),
    ];

    public static IReadOnlyList<IPostDownloadValidator> BuildPostDownloadValidators() =>
    [
        new GoFilePostDownloadValidator(),
        new EHentaiMinimumSizeValidator(),
    ];
    
    public static IReadOnlyList<IExternalToolDownloadStrategy> BuildExternalToolStrategies() =>
        ReflectionDiscovery.DiscoverImplementations<IExternalToolDownloadStrategy>();
}