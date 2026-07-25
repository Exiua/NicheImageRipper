using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.Utility;

namespace NicheImageRipper.Core.FileDownloading.RequestHeaderModifier;

public sealed class RedgifsAuthHeaderModifier : IRequestHeaderModifier
{
    public bool AppliesTo(string url, FileLink link, DownloadContext context) => url.Contains("redgifs");

    public async Task<IReadOnlyDictionary<string, string?>> ApplyAsync(Dictionary<string, string> requestHeaders,
                                                                       string url, FileLink link, DownloadContext context, CancellationToken cancellationToken)
    {
        var token = await TokenManager.Instance.GetToken(TokenKey.Redgifs, cancellationToken);
        requestHeaders[RequestHeaderKeys.Authorization] = $"Bearer {token.Value}";
        return new Dictionary<string, string?> { [RequestHeaderKeys.Authorization] = null };
    }
}