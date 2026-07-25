using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;

namespace NicheImageRipper.Core.FileDownloading;

public interface IFileDownloadStrategy
{
    LinkInfo HandlesLinkInfo { get; }
    Task<DownloadResult> DownloadAsync(FileLink link, string imagePath, DownloadContext context, CancellationToken cancellationToken);
}