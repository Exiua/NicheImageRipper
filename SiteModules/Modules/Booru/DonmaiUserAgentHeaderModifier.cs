using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.FileDownloading;
using NicheImageRipper.Core.FileDownloading.FileDownloadStrategies;
using NicheImageRipper.Core.Utility;

namespace NicheImageRipper.SiteModules.Modules.Booru;

public sealed class DonmaiUserAgentHeaderModifier : IRequestHeaderModifier
{
    // Matches original: this branch checked fileLink.Url, not the ambient url param (unlike Redgifs).
    public bool AppliesTo(string url, FileLink link, DownloadContext context) => link.Url.Contains("donmai.us");

    public Task<IReadOnlyDictionary<string, string?>> ApplyAsync(Dictionary<string, string> requestHeaders,
                                                                 string url, FileLink link, DownloadContext context,
                                                                 CancellationToken cancellationToken)
    {
        requestHeaders[RequestHeaderKeys.UserAgent] = "NicheImageRipper";

        IReadOnlyDictionary<string, string?> restoreMap =
            new Dictionary<string, string?> { [RequestHeaderKeys.UserAgent] = ConfigAccess.Config.UserAgent };
        return Task.FromResult(restoreMap);
    }
}