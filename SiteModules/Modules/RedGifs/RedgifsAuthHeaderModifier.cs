using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.FileDownloading;
using NicheImageRipper.Sdk.TokenManagement;
using NicheImageRipper.Sdk.Utility;

namespace NicheImageRipper.SiteModules.Modules.RedGifs;

public sealed class RedgifsAuthHeaderModifier : IRequestHeaderModifier
{
    public bool AppliesTo(string url, FileLink link, DownloadContext context) => url.Contains("redgifs");

    public async Task<IReadOnlyDictionary<string, string?>> ApplyAsync(Dictionary<string, string> requestHeaders,
                                                                       string url, FileLink link,
                                                                       DownloadContext context,
                                                                       CancellationToken cancellationToken)
    {
        var token = await TokenManager.Instance.GetOrGenerate(RedGifsParser.ParserName,
            RedGifsParser.GenerateRedgifsToken, cancellationToken);
        requestHeaders[RequestHeaderKeys.Authorization] = $"Bearer {token.Value}";
        return new Dictionary<string, string?> { [RequestHeaderKeys.Authorization] = null };
    }
}