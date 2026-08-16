using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.FileDownloading;
using NicheImageRipper.Core.FileDownloading.FileDownloadStrategies;

namespace NicheImageRipper.SiteModules.Modules.GoFile;

public class GoFileDownloadStrategy : IFileDownloadStrategy
{
    private readonly GenericHttpDownloadStrategy _generic = new();

    public IEnumerable<LinkInfo> HandlesLinkInfo => [GoFileLinkInfo.GoFile];
    public bool SupportsPostProcessing => true; // matches GenericHttpDownloadStrategy's default

    public Task<DownloadResult> DownloadAsync(FileLink link, string imagePath, DownloadContext context,
                                              CancellationToken cancellationToken = default)
    {
        return _generic.DownloadAsync(link, imagePath, context, cancellationToken);
    }
}