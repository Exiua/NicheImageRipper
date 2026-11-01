using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.FileDownloading;

namespace NicheImageRipper.SiteModules.Modules.IframeMediaDelivery;

public sealed class IframeMediaDownloadStrategy : IFileDownloadStrategy
{
    private const int RetryCount = 4;

    public IEnumerable<LinkInfo> HandlesLinkInfo => [IframeMediaDeliveryLinkInfo.IframeMedia];

    public async Task<DownloadResult> DownloadAsync(FileLink link, string imagePath, DownloadContext context,
                                                    CancellationToken cancellationToken = default)
    {
        var parentPathInfo = Directory.GetParent(imagePath)!;
        var parentPath = parentPathInfo.FullName;
        Directory.CreateDirectory(parentPath);

        for (var i = 0; i < RetryCount; i++)
        {
            try
            {
                var video = new BunnyVideoDrm(
                    referer: link.Url,
                    embedUrl: link.Referer!,
                    name: Path.GetFileName(imagePath).Split('.')[0],
                    path: parentPath
                );
                await video.Download(cancellationToken);
                foreach (var f in parentPathInfo.EnumerateFiles(".*"))
                {
                    f.Delete();
                }

                return DownloadResult.Success();
            }
            catch (UnauthorizedAccessException)
            {
                if (i == RetryCount - 1)
                {
                    return DownloadResult.Failed("Unauthorized after retries");
                }
            }
        }

        return DownloadResult.Failed();
    }
}