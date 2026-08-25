using System.Text.Json;




using Sdk.Configuration;
using Sdk.DataStructures;
using Sdk.Exceptions;
using Sdk.Utility;

namespace NicheImageRipper.SiteModules.Modules.GoFile;

public sealed class GoFileCookieHeaderModifier : IRequestHeaderModifier
{
    private static GeneralConfig Config => Sdk.Configuration.Config.Instance;
    
    public bool AppliesTo(string url, FileLink link, DownloadContext context) => link.LinkInfo == GoFileLinkInfo.GoFile;

    public Task<IReadOnlyDictionary<string, string?>> ApplyAsync(Dictionary<string, string> requestHeaders,
                                                                 string url, FileLink link, DownloadContext context, CancellationToken cancellationToken)
    {
        var oldCookies = requestHeaders[RequestHeaderKeys.Cookie];
        var config = Config.Custom.GetValueOrDefault("gofile").Deserialize<GoFileConfig>();
        if (config is null)
        {
            throw new RipperException("GoFile configuration is missing. Please ensure that the GoFile configuration is properly set in the config file.");
        }
        
        var cookieValue = config.AccountToken;
        requestHeaders[RequestHeaderKeys.Cookie] = $"accountToken={cookieValue}";

        IReadOnlyDictionary<string, string?> restoreMap =
            new Dictionary<string, string?> { [RequestHeaderKeys.Cookie] = oldCookies };
        return Task.FromResult(restoreMap);
    }
}