using System.Net;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Exceptions;
using NicheImageRipper.Core.FileDownloading;

namespace NicheImageRipper.SiteModules.Modules.EHentai;

public sealed class EHentaiExpiredLinkErrorHandler : IDownloadErrorHandler
{
    private const int MillisecondsInSecond = 1000;

    public bool AppliesTo(HttpStatusCode statusCode, string url, FileLink link, DownloadContext context) =>
        statusCode == HttpStatusCode.Forbidden && context.SiteName == "e-hentai";

    public async Task<DownloadResult> HandleAsync(HttpResponseMessage response, string url, FileLink link,
                                                  bool generatingManually, DownloadContext context, CancellationToken cancellationToken)
    {
        context.Logger.Information("E-Hentai URL expired, trying to update links...");
        await Task.Delay(10 * MillisecondsInSecond, cancellationToken);
        throw new UrlExpiredException(context.SiteName);
    }
}