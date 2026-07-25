using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;

namespace NicheImageRipper.Core.FileDownloading.FileDownloadStrategies;

public sealed class M3U8YtDlpDownloadStrategy : IFileDownloadStrategy
{
    private readonly ObfuscatedM3U8DownloadStrategy _obfuscatedFallback = new();

    public IEnumerable<LinkInfo> HandlesLinkInfo => [LinkInfo.M3U8YtDlp];

    public async Task<DownloadResult> DownloadAsync(FileLink link, string imagePath, DownloadContext context,
                                                    CancellationToken cancellationToken = default)
    {
        var success = await YtDlpRunner.RunYtDlp(link, imagePath, "Starting yt-dlp download",
            "yt-dlp download finished", context, cancellationToken);
        return success
            ? DownloadResult.Success()
            : await _obfuscatedFallback.DownloadAsync(link, imagePath, context, cancellationToken);
    }
}