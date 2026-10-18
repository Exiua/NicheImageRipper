using System.Net;
using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Exceptions;
using NicheImageRipper.Sdk.FileDownloading;

namespace NicheImageRipper.SiteModules.Modules.Pornhub;

/// <summary>
///     Pornhub's stream links expire: a 404 or 410 part-way through means the links need refreshing, not that the
///     video is gone.
/// </summary>
public sealed class PornhubExpiredLinkHandler : IDownloadExceptionHandler
{
    public bool AppliesTo(Exception exception, FileLink link, DownloadContext context) =>
        context.SiteName == PornhubParser.ParserName &&
        exception is HttpRequestException { StatusCode: HttpStatusCode.Gone or HttpStatusCode.NotFound };

    public DownloadResult Handle(Exception exception, FileLink link, DownloadContext context) =>
        throw new UrlExpiredException(context.SiteName);
}
