using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.FileDownloading.FileDownloadStrategies;
using NicheImageRipper.Core.Utility;

namespace NicheImageRipper.Core.FileDownloading.RequestHeaderModifier;

public sealed class GoFileCookieHeaderModifier : IRequestHeaderModifier
{
    public bool AppliesTo(string url, FileLink link, DownloadContext context) => link.LinkInfo == LinkInfo.GoFile;

    public Task<IReadOnlyDictionary<string, string?>> ApplyAsync(Dictionary<string, string> requestHeaders,
                                                                 string url, FileLink link, DownloadContext context, CancellationToken cancellationToken)
    {
        var oldCookies = requestHeaders[RequestHeaderKeys.Cookie];
        var cookieValue = ConfigAccess.Config.Custom.GoFile.AccountToken;
        requestHeaders[RequestHeaderKeys.Cookie] = $"accountToken={cookieValue}";

        IReadOnlyDictionary<string, string?> restoreMap =
            new Dictionary<string, string?> { [RequestHeaderKeys.Cookie] = oldCookies };
        return Task.FromResult(restoreMap);
    }
}