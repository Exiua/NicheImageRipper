using NicheImageRipper.Sdk.DataStructures;

namespace NicheImageRipper.Sdk.FileDownloading;

public interface IPostDownloadValidator
{
    bool AppliesTo(FileLink link, DownloadContext context);
    Task<DownloadResult> ValidateAsync(string filePath, FileLink link, DownloadContext context,
                                       CancellationToken cancellationToken);
}