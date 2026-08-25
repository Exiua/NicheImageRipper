using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using Sdk.DataStructures;
using Sdk.FileDownloading;

namespace NicheImageRipper.Core.FileDownloading.FileDownloadStrategies;

public sealed class TextDownloadStrategy : IFileDownloadStrategy
{
    public IEnumerable<LinkInfo> HandlesLinkInfo => [ LinkInfo.Text ];

    public async Task<DownloadResult> DownloadAsync(FileLink link, string imagePath, DownloadContext context,
                                                    CancellationToken cancellationToken = default)
    {
        await File.AppendAllTextAsync(imagePath, link.Url + "\n", cancellationToken);
        return DownloadResult.Success();
    }
}