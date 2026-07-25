using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;

namespace NicheImageRipper.Core.FileDownloading.FileDownloadStrategies;

public sealed class YoutubeVideoDownloadStrategy : IFileDownloadStrategy
{
    public IEnumerable<LinkInfo> HandlesLinkInfo => [LinkInfo.YoutubeVideo];

    public async Task<DownloadResult> DownloadAsync(FileLink link, string imagePath, DownloadContext context,
                                                    CancellationToken cancellationToken = default)
    {
        var success = await YtDlpRunner.RunYtDlp(link, imagePath, "Starting youtube-dl download",
            "youtube-dl download finished", cancellationToken);
        return success ? DownloadResult.Success() : DownloadResult.Failed();
    }
}