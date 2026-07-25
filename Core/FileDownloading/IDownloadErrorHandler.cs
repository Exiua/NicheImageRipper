using System.Net;
using NicheImageRipper.Core.DataStructures;

namespace NicheImageRipper.Core.FileDownloading;

public interface IDownloadErrorHandler
{
    bool AppliesTo(HttpStatusCode statusCode, string url, FileLink link, DownloadContext context);

    /// <returns>Retry to re-attempt now that state (e.g. link.Url) may have been adjusted; may also throw to unwind to a higher-level recovery path (e.g. EHentaiUrlExpiredException).</returns>
    Task<DownloadResult> HandleAsync(HttpResponseMessage response, string url, FileLink link,
                                     bool generatingManually, DownloadContext context, CancellationToken cancellationToken);
}