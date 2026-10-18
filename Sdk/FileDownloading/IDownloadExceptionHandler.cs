using NicheImageRipper.Sdk.DataStructures;

namespace NicheImageRipper.Sdk.FileDownloading;

/// <summary>
///     Site-specific handling for an exception thrown while downloading, for failures that never produce an HTTP
///     response to inspect (so <see cref="IDownloadErrorHandler"/> can't see them), such as an HLS segment request
///     failing inside a downloader. Strategies consult <see cref="DownloadContext.ExceptionHandlers"/> before treating
///     the exception as a generic failure.
/// </summary>
public interface IDownloadExceptionHandler
{
    bool AppliesTo(Exception exception, FileLink link, DownloadContext context);

    /// <returns>The outcome to report instead of a generic failure.</returns>
    /// <remarks>May throw instead to unwind to a higher-level recovery path (e.g. <c>UrlExpiredException</c>).</remarks>
    DownloadResult Handle(Exception exception, FileLink link, DownloadContext context);
}
