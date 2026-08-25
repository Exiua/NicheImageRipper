using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using Sdk.DataStructures;
using Sdk.FileDownloading;

namespace NicheImageRipper.Core.FileDownloading.FileDownloadStrategies;

public sealed class MpegDashDownloadStrategy : IFileDownloadStrategy
{
    public IEnumerable<LinkInfo> HandlesLinkInfo => [LinkInfo.MpegDash];

    public async Task<DownloadResult> DownloadAsync(FileLink link, string imagePath, DownloadContext context,
                                                    CancellationToken cancellationToken = default)
    {
        var parent = Directory.GetParent(imagePath)!.FullName;
        var filename = Path.GetFileName(imagePath);
        var cmd = new[] { "-P", $"\"{parent}\"", link.Url, "-o", filename };
        var (exitCode, _, _) = await ProcessRunner.RunSubprocess("yt-dlp", cmd,
            startMessage: "Starting youtube-dl download", endMessage: "youtube-dl download finished",
            cancellationToken: cancellationToken);
        return exitCode == 0 ? DownloadResult.Success() : DownloadResult.Failed();
    }
}