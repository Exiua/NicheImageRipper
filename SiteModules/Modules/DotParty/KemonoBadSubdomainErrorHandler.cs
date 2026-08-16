using System.Net;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Exceptions;
using NicheImageRipper.Core.FileDownloading;

namespace NicheImageRipper.SiteModules.Modules.DotParty;

public sealed class KemonoBadSubdomainErrorHandler : IDownloadErrorHandler
{
    public bool AppliesTo(HttpStatusCode statusCode, string url, FileLink link, DownloadContext context) =>
        statusCode == HttpStatusCode.Forbidden && context.SiteName == "kemono" &&
        !url.Contains(".psd") && url.Contains("kemono");

    public Task<DownloadResult> HandleAsync(HttpResponseMessage response, string url, FileLink link,
                                            bool generatingManually, DownloadContext context, CancellationToken cancellationToken)
    {
        context.Logger.Information("Wrong subdomain, trying again...");
        throw new BadSubdomainException();
    }
}