using Sdk.DataStructures;

namespace Sdk.FileDownloading;

public interface IExternalToolDownloadStrategy
{
    bool AppliesTo(string siteName);
    Task<DownloadResult> DownloadAsync(RipInfo folderInfo, string fullPath, DownloadContext context,
                                       CancellationToken cancellationToken = default);
}