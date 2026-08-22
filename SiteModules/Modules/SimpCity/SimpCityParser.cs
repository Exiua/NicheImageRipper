using HtmlAgilityPack;
using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Exceptions;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using NicheImageRipper.Core.SiteParsing.HtmlParsers;
using NicheImageRipper.Core.Utility;
using OpenQA.Selenium;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.SimpCity;

public class SimpCityParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "simpcity";
    public static string[] SupportedUrls => ["https://simpcity.su/", "https://www.simpcity.su/"];

    private const string CacheFile = "simpcitycache.json";
    private const string RateLimitTitle = "SimpCity - Rate Limit";
    private const string CookieName = "dontlikebots_user";

    public SimpCityParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders,
                          FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager,
        requestHeaders, IHtmlParser.GetFilenameScheme<SimpCityParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for simpcity.su and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var (images, resolveLists, dirName) = await LoadOrParsePosts();
        await ResolveEmbeddedLinks(images, resolveLists, cancellationToken);

        File.Delete(CacheFile);
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }

    /// <summary>Loads cached post data for the current URL if present, otherwise parses the page and caches the result.</summary>
    private async Task<(List<StringFileLinkWrapper> Images, List<IndexedContainer<List<string>>> ResolveLists, string DirName)>
        LoadOrParsePosts()
    {
        var currentUrl = CurrentUrl;

        if (!File.Exists(CacheFile))
        {
            Logger.Debug("Cache file not found, parsing page");
            return await ParsePostsAndCache(currentUrl);
        }

        Logger.Debug("Cache file found, loading cached data");
        var cache = JsonUtility.Deserialize<Cache>(CacheFile) ?? throw new RipperException("Cache file is empty");
        if (cache.Url != CurrentUrl)
        {
            Logger.Debug("Cached data not present for current url, parsing page");
            return await ParsePostsAndCache(currentUrl);
        }

        Logger.Debug("Cached data loaded");
        return (cache.Images, cache.ResolveLists, cache.DirName);
    }

    private async Task<(List<StringFileLinkWrapper>, List<IndexedContainer<List<string>>>, string)> ParsePostsAndCache(
        string currentUrl)
    {
        var (images, resolveLists, dirName) = await ParsePosts();
        var cache = new Cache
        {
            Url = currentUrl,
            DirName = dirName,
            ResolveLists = resolveLists,
            Images = images
        };
        JsonUtility.Serialize(CacheFile, cache);
        return (images, resolveLists, dirName);
    }

    /// <summary>Resolves every embedded link found across the parsed posts (e.g. Bunkr/GoFile/PixelDrain links)
    /// into their actual downloadable file links, inserting the results into <paramref name="images"/> in place.</summary>
    private async Task ResolveEmbeddedLinks(List<StringFileLinkWrapper> images,
                                            List<IndexedContainer<List<string>>> resolveLists,
                                            CancellationToken cancellationToken)
    {
        var offset = 0;
        var resolved = 1;
        var total = resolveLists.Sum(list => list.Value.Count);

        foreach (var (resolveList, i) in resolveLists)
        {
            var index = i + offset;
            foreach (var link in resolveList)
            {
                Logger.Information("Resolving link {Resolved} of {Total}: {Link}", resolved, total, link);
                resolved++;

                var info = await ResolveLink(link, cancellationToken);
                Logger.Debug("Found {Count} files", info.Urls.Count);

                images.InsertRange(index, info.Urls.ToStringImageLinks());
                offset += info.Urls.Count;
                await Sleep(250, cancellationToken: cancellationToken);
            }
        }
    }

    /// <summary>Resolves a single embedded link via the parser registered for its site, retrying once against
    /// a regenerated driver if the WebDriver appears to have become unresponsive.</summary>
    private async Task<RipInfo> ResolveLink(string link, CancellationToken cancellationToken)
    {
        var parser = CreateParser(link);

        try
        {
            return await parser.Parse(link, cancellationToken: cancellationToken);
        }
        catch (WebDriverTimeoutException)
        {
            Logger.Warning("WebDriver unresponsive, retrying");
            // Assume driver is dead and unreachable
            // TODO: Find a better way to handle this
            return await ReTryParse(link, parser, cancellationToken);
        }
        catch (WebDriverException e) when (e.Message.Contains("The HTTP request to the remote WebDriver server for URL"))
        {
            Logger.Warning("WebDriver unresponsive, retrying");
            // TODO: Find a better way to handle this
            return await ReTryParse(link, parser, cancellationToken);
        }
    }

    private async Task<(List<StringFileLinkWrapper>, List<IndexedContainer<List<string>>>, string)> ParsePosts()
    {
        var soup = await LoadFirstPage();
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='p-title-value']").InnerText;
        Logger.Debug("Directory name: {DirName}", dirName);

        var lazyLoadArgs = new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250,
            ScrollPauseTime = 1000
        };

        var images = new List<StringFileLinkWrapper>();
        var resolveLists = new List<IndexedContainer<List<string>>>();
        var cookieJar = Driver.GetCookieJar();

        while (true)
        {
            if (await HandleRateLimitIfPresent())
            {
                continue;
            }

            #if DEBUG
            Driver.TakeDebugScreenshot();
            #endif

            ParsePostsOnPage(soup, images, resolveLists);

            var nextPage = soup.SelectSingleNode("//a[@class='pageNav-jump pageNav-jump--next']");
            if (nextPage is null)
            {
                break;
            }

            var nextPageUrl = $"https://simpcity.su{nextPage.GetHref()}";
            Logger.Information("Parsing page: {NextPageUrl}", nextPageUrl);
            soup = await Soupify(nextPageUrl, lazyLoadArgs: lazyLoadArgs, delay: 1000, cookies: cookieJar);
        }

        return (images, resolveLists, dirName);
    }

    /// <summary>Solves the page's captcha/cookie challenge and loads the first page of posts.</summary>
    private async Task<HtmlNode> LoadFirstPage()
    {
        if (CurrentUrl.Contains("//www."))
        {
            CurrentUrl = CurrentUrl.Replace("//www.", "//");
        }

        Logger.Debug("Getting user cookie");
        var cookies = Config.Cookies.GetValueOrDefault(ParserName, []);
        if (cookies.Length == 0)
        {
            throw new MissingCookieException(CookieName, ParserName);
        }

        var cookieValue = cookies[0];
        Logger.Debug("Solving captcha and parsing page");
        var userCookie = new Dictionary<string, string>
        {
            ["name"] = CookieName,
            ["value"] = cookieValue
        };
        var soup = await SolveParseAddCookies(cookies: [userCookie]);

        Logger.Debug("Adding cookie to driver");
        Driver.SetCookie(CookieName, cookieValue);

        return soup;
    }

    /// <summary>If the page is currently rate-limited, waits and refreshes. Returns true if a wait/refresh occurred.</summary>
    private async Task<bool> HandleRateLimitIfPresent()
    {
        if (Driver.Title != RateLimitTitle)
        {
            return false;
        }

        Logger.Warning("Rate limited, waiting 30 seconds");
        await Sleep(30000);
        Driver.Refresh();
        return true;
    }

    /// <summary>Extracts images, videos, and resolvable embedded links from every post on the current page,
    /// appending results into <paramref name="images"/>/<paramref name="resolveLists"/> in place.</summary>
    private void ParsePostsOnPage(HtmlNode soup, List<StringFileLinkWrapper> images,
                                  List<IndexedContainer<List<string>>> resolveLists)
    {
        var posts = soup.SelectSingleNodeOrThrow("//div[@class='block-body js-replyNewMessageContainer']")
                        .SelectNodesSafe("./article");

        foreach (var post in posts)
        {
            var content = post.SelectSingleNodeOrThrow(".//div[@class='bbWrapper']");

            var imgs = content.SelectNodesSafe(".//img").Select(img => img.GetSrc().Remove(".md"))
                              .Where(url => !url.StartsWith("data:image/gif")) // Ignore emojis
                              .ToStringImageLinks();
            images.AddRange(imgs);

            var vids = content.SelectNodesSafe(".//video").Select(VideoResolver).ToStringImageLinks();
            images.AddRange(vids);

            var index = images.Count;
            var resolve = ExtractResolvableLinks(content);
            if (resolve.Count != 0)
            {
                resolveLists.Add(new IndexedContainer<List<string>>(resolve, index));
            }
        }
    }

    /// <summary>Finds every link in a post's content that a registered parser can resolve, plus any
    /// Cyberdrop/Saint2 iframe embeds (identified by SimpCity's own "saint-iframe" markup class).</summary>
    private List<string> ExtractResolvableLinks(HtmlNode content)
    {
        var links = content.SelectNodesSafe(".//a");
        #if DEBUG
        var rawLinks = links.Select(link => link.GetNullableHref()).OfType<string>().ToList();
        var resolve = rawLinks.Where(IsResolvableLink).ToList();
        foreach (var link in rawLinks.Except(resolve))
        {
            Logger.Debug("Unhandled link: {Link}", link);
        }
        #else
        var resolve = links.Select(link => link.GetNullableHref())
                            .OfType<string>()
                            .Where(IsResolvableLink)
                            .ToList();
        #endif

        var iframes = content.SelectNodesSafe(".//iframe[@class='saint-iframe']")
                             .Select(iframe => iframe.GetSrc()); // cyberdrop and saint2
        resolve.AddRange(iframes);

        return resolve;
    }

    private static bool IsResolvableLink(string url)
    {
        try
        {
            var host = new Uri(url).Host;
            return HtmlParserFactory.DelegatableDomains.Any(host.EndsWith);
        }
        catch (UriFormatException)
        {
            return false;
        }
    }

    private static string VideoResolver(HtmlNode video)
    {
        var src = video.GetNullableSrc();
        if (src is not null)
        {
            return src.Remove("-mobile");
        }

        var source = video.SelectSingleNodeOrThrow(".//source");
        src = source.GetSrc();
        if (!src.Contains("saint2.pk"))
        {
            return src;
        }

        var grandParent = video.ParentNode.ParentNode;
        var downloadLink = grandParent.SelectSingleNodeOrThrow(".//a[@class='plyr__controls__item plyr__control']")
                                      .GetHref();
        var id = downloadLink.Split("/d/")[^1];
        return $"https://simp2.saint2.pk/api/download.php?file={id}";
    }

    private Task<RipInfo> ReTryParse(string link, ParameterizedHtmlParser parser,
                                     CancellationToken cancellationToken = default)
    {
        WebDriver.RegenerateDriver();
        // Driver.Url = link;
        // var cookieJar = Driver.GetCookieJar();
        // foreach(var cookie in cookies)
        // {
        //     cookieJar.AddCookie(cookie);
        // }
        // Parse w/link will internally reload the page
        return parser.Parse(link, cancellationToken: cancellationToken);
    }

    private class Cache
    {
        public string Url { get; set; } = null!;
        public string DirName { get; set; } = null!;
        public List<IndexedContainer<List<string>>> ResolveLists { get; set; } = null!;
        public List<StringFileLinkWrapper> Images { get; set; } = null!;
    }
}