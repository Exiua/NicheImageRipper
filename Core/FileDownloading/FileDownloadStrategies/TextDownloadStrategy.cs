using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;

namespace NicheImageRipper.Core.FileDownloading.FileDownloadStrategies;

public sealed class TextDownloadStrategy : IFileDownloadStrategy
{
    public LinkInfo HandlesLinkInfo => LinkInfo.Text;

    public async Task<DownloadResult> DownloadAsync(FileLink link, string imagePath, DownloadContext context,
                                                    CancellationToken cancellationToken = default)
    {
        await File.AppendAllTextAsync(imagePath, link.Url + "\n", cancellationToken);
        return DownloadResult.Success();
    }
}