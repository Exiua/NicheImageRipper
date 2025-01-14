namespace Core.Enums;

public enum Booru
{
    Danbooru,
    Gelbooru,
    Rule34,
    Yandere,
}

public class BooruMetadata
{
    public required string SiteName { get; init; }
    public required string BaseUrl { get; init; }
    public required string PageParameterName { get; init; }
    public int StartingPageIndex { get; init; }
    public int Limit { get; init; }
    public Dictionary<string, string>? Headers { get; init; }
    public string[]? JsonObjectNavigation { get; init; }
}

public static class BooruExtensionMethods
{
    public static BooruMetadata GetMetadata(this Booru site)
    {
        return site switch
        {
            Booru.Danbooru => new BooruMetadata
            {
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
                SiteName = "Gelbooru",
                BaseUrl = "https://gelbooru.com/index.php?page=dapi&s=post&q=index&json=1&",
                PageParameterName = "pid",
                StartingPageIndex = 0,
                Limit = 100,
                JsonObjectNavigation = ["post"]
            },
            Booru.Rule34 => new BooruMetadata
            {
                SiteName = "Rule34",
                BaseUrl = "https://api.rule34.xxx/index.php?page=dapi&s=post&q=index&json=1&",
                PageParameterName = "pid",
                StartingPageIndex = 0,
                Limit = 1000
            },
            Booru.Yandere => new BooruMetadata
            {
                SiteName = "Yande.re",
                BaseUrl = "https://yande.re/post.json?",
                PageParameterName = "page",
                StartingPageIndex = 1,
                Limit = 100
            },
            _ => throw new ArgumentOutOfRangeException(nameof(site), site, null)
        };
    }
}