using NicheImageRipper.Core.Configuration;
using NicheImageRipper.Core.Exceptions;

namespace NicheImageRipper.Core.Enums;

public enum Booru
{
    Danbooru,
    Gelbooru,
    Rule34,
    Yandere,
    E621,
}

public class BooruMetadata
{
    public required Booru Booru { get; init; }
    public required string SiteName { get; init; }
    // BaseUrl must end with either ? or &
    public required string BaseUrl { get; init; }
    public required string PageParameterName { get; init; }
    public int StartingPageIndex { get; init; }
    public int Limit { get; init; }
    public Dictionary<string, string>? Headers { get; init; }
    public string[]? JsonObjectNavigationToArray { get; init; }
    public bool ArrayMayNotExist { get; init; } = false;
    public string[] JsonObjectNavigationToUrl { get; init; } = ["file_url"];
    public int Delay { get; init; } = 250;

    private static GeneralConfig Config => Configuration.Config.Instance;

    // The full base url must end with either ? or &
    public string GetFullBaseUrl()
    {
        switch (Booru)
        {
            case Booru.Danbooru:
            {
                var (username, password) = Config.Logins.GetValueOrDefault("danbooru").Deconstruct();
                if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                {
                    return $"{BaseUrl}api_key={password}&login={username}&";
                }

                return BaseUrl;
            }
            case Booru.Gelbooru:
            {
                var (username, password) = Config.Logins.GetValueOrDefault("gelbooru").Deconstruct();
                if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                {
                    return $"{BaseUrl}api_key={password}&user_id={username}&";
                }

                throw new ApiKeyRequired("Gelbooru");
            }
            case Booru.Rule34:
            {
                var (username, password) = Config.Logins.GetValueOrDefault("rule34").Deconstruct();
                if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                {
                    return $"{BaseUrl}api_key={password}&user_id={username}&";
                }
                
                throw new ApiKeyRequired("Rule34");
            }
            case Booru.Yandere:
            {
                var (username, password) = Config.Logins.GetValueOrDefault("yandere").Deconstruct();
                // TODO: Find out how to authenticate with Yandere if it's possible
                // Signups are disabled (as of 2025-05-06), so unable to test this
                return BaseUrl;
            }
            case Booru.E621:
            {
                var (username, password) = Config.Logins.GetValueOrDefault("e621").Deconstruct();
                if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                {
                    return $"{BaseUrl}login={username}&api_key={password}&";
                }
                
                return BaseUrl;
            }
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}

public static class BooruExtensionMethods
{
    public static BooruMetadata GetMetadata(this Booru site)
    {
        return site switch
        {
            Booru.Danbooru => new BooruMetadata
            {
                Booru = site,
                SiteName = "Danbooru",
                BaseUrl = "https://danbooru.donmai.us/posts.json?",
                PageParameterName = "page",
                StartingPageIndex = 1,
                Limit = 200,
                Headers = new Dictionary<string, string>
                {
                    {"User-Agent", "NicheImageRipper"}
                }
            },
            Booru.Gelbooru => new BooruMetadata
            {
                Booru = site,
                SiteName = "Gelbooru",
                BaseUrl = "https://gelbooru.com/index.php?page=dapi&s=post&q=index&json=1&",
                PageParameterName = "pid",
                StartingPageIndex = 0,
                Limit = 100,
                JsonObjectNavigationToArray = ["post"],
                ArrayMayNotExist = true
            },
            Booru.Rule34 => new BooruMetadata
            {
                Booru = site,
                SiteName = "Rule34",
                BaseUrl = "https://api.rule34.xxx/index.php?page=dapi&s=post&q=index&json=1&",
                PageParameterName = "pid",
                StartingPageIndex = 0,
                Limit = 1000
            },
            Booru.Yandere => new BooruMetadata
            {
                Booru = site,
                SiteName = "Yande.re",
                BaseUrl = "https://yande.re/post.json?",
                PageParameterName = "page",
                StartingPageIndex = 1,
                Limit = 100,
                Headers = new Dictionary<string, string>
                {
                    {"User-Agent", Config.Instance.UserAgent}
                }
            },
            Booru.E621 => new BooruMetadata
            {
                Booru = site,
                SiteName = "E621",
                BaseUrl = "https://e621.net/posts.json?",
                PageParameterName = "page",
                StartingPageIndex = 1,
                Limit = 320,
                Headers = new Dictionary<string, string>
                {
                    {"User-Agent", "NicheImageRipper"}
                },
                JsonObjectNavigationToArray = ["posts"],
                JsonObjectNavigationToUrl = ["file", "url"],
                Delay = 520
            },
            _ => throw new ArgumentOutOfRangeException(nameof(site), site, null)
        };
    }
}