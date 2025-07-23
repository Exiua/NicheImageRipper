using Common.ExtensionMethods;
using System.Collections.Frozen;
using Core.DataStructures;
using Core.Enums;
using Core.Exceptions;
using Core.ExtensionMethods;
using Core.Utility;
using HtmlAgilityPack;
using OpenQA.Selenium;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class SimpCityParser : HtmlParser
{
    private const string CacheFile = "simpcitycache.json";

    public SimpCityParser(WebDriver driver, Dictionary<string, string> requestHeaders,
                          FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for simpcity.su and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var currentUrl = CurrentUrl;
        Log.Debug("Creating resolvable url map");
        var resolvableMap = CreateResolveMap();
        
        Cache cache;
        List<StringImageLinkWrapper> images;
        List<IndexedContainer<List<string>>> resolveLists;
        string dirName;
        
        if (File.Exists(CacheFile))
        {
            Log.Debug("Cache file found, loading cached data");
            cache = JsonUtility.Deserialize<Cache>(CacheFile) ?? throw new RipperException("Cache file is empty");
            if (cache.Url == CurrentUrl)
            {
                Log.Debug("Cached data loaded");
                images = cache.Images;
                resolveLists = cache.ResolveLists;
                dirName = cache.DirName;
            }
            else
            {
                Log.Debug("Cached data not present for current url, parsing page");
                (images, resolveLists, dirName) = await ParsePosts(resolvableMap);

                cache = new Cache
                {
                    Url = currentUrl,
                    DirName = dirName,
                    ResolveLists = resolveLists,
                    Images = images
                };
                JsonUtility.Serialize(CacheFile, cache);
            }
        }
        else
        {
            Log.Debug("Cache file not found, parsing page");
            (images, resolveLists, dirName) = await ParsePosts(resolvableMap);

            cache = new Cache
            {
                Url = currentUrl,
                DirName = dirName,
                ResolveLists = resolveLists,
                Images = images
            };
            JsonUtility.Serialize(CacheFile, cache);
        }

        #region Resolve Links

        var offset = 0;
        var resolved = 1;
        var total = resolveLists.Sum(list => list.Value.Count);
        foreach (var (resolveList, i) in resolveLists)
        {
            var index = i + offset;
            foreach (var link in resolveList)
            {
                Log.Information("Resolving link {Resolved} of {Total}: {Link}", resolved, total, link);
                resolved++;
                ParameterizedHtmlParser? parser = null;
                foreach (var (urlPart, p) in resolvableMap)
                {
                    if (link.Contains(urlPart))
                    {
                        parser = p;
                        break;
                    }
                }

                if (parser is null)
                {
                    throw new RipperException($"Link bypassed filter: {link}");
                }

                RipInfo info;
                try
                {
                    info = await parser.Parse(link);
                    Log.Debug("Found {Count} files", info.Urls.Count);
                }
                catch (WebDriverTimeoutException)
                {
                    Log.Warning("WebDriver unresponsive, retrying");
                    // Assume driver is dead and unreachable
                    // TODO: Find a better way to handle this
                    info = await ReTryParse(link, parser);
                }
                catch (WebDriverException e) when (e.Message.Contains(
                                                       "The HTTP request to the remote WebDriver server for URL"))
                {
                    Log.Warning("WebDriver unresponsive, retrying");
                    // Assume driver is dead and unreachable
                    // TODO: Find a better way to handle this
                    info = await ReTryParse(link, parser);
                }

                images.InsertRange(index, info.Urls.ToStringImageLinks());
                offset += info.Urls.Count;
                await Sleep(250);
            }
        }

        #endregion

        File.Delete(CacheFile);
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }

    private async Task<(List<StringImageLinkWrapper>, List<IndexedContainer<List<string>>>, string)> ParsePosts(Dictionary<string, ParameterizedHtmlParser> resolvableMap)
    {
        if (CurrentUrl.Contains("//www."))
        {
            CurrentUrl = CurrentUrl.Replace("//www.", "//");
        }

        Log.Debug("Getting user cookie");
        var cookieValue = Config.Cookies.SimpCity;
        Log.Debug("Solving captcha and parsing page");
        var userCookie = new Dictionary<string, string>
        {
            ["name"] = "dontlikebots_user",
            ["value"] = cookieValue
        };
        var soup = await SolveParseAddCookies(cookies: [userCookie]);
        Log.Debug("Adding cookie to driver");
        Driver.SetCookie("dontlikebots_user", cookieValue);
        var cookieJar = Driver.GetCookieJar();

        Log.Debug("Parsing page");
        var dirName = soup.SelectSingleNode("//h1[@class='p-title-value']").InnerText;
        Log.Debug("Directory name: {DirName}", dirName);
        
        var lazyLoadArgs = new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250,
            ScrollPauseTime = 1000
        };

        var images = new List<StringImageLinkWrapper>();
        var resolveLists = new List<IndexedContainer<List<string>>>();
        while (true)
        {
            if (Driver.Title == "SimpCity - Rate Limit")
            {
                Log.Warning("Rate limited, waiting 30 seconds");
                await Sleep(30000);
                Driver.Refresh();
                continue;
            }

            #if DEBUG
            Driver.TakeDebugScreenshot();
            #endif

            var posts = soup.SelectSingleNode("//div[@class='block-body js-replyNewMessageContainer']")
                            .SelectNodes("./article");
            foreach (var post in posts)
            {
                var content = post.SelectSingleNode(".//div[@class='bbWrapper']");
                var imgs = content.SelectNodesSafe(".//img")
                                  .Select(img => img.GetSrc().Remove(".md"))
                                  .Where(url => !url.StartsWith("data:image/gif")) // Ignore emojis
                                  .ToStringImageLinks();
                images.AddRange(imgs);
                var vids = content.SelectNodesSafe(".//video")
                                  .Select(VideoResolver)
                                  .ToStringImageLinks();
                images.AddRange(vids);
                var index = images.Count;
                var links = content.SelectNodesSafe(".//a");
                List<string> resolve;
                #if DEBUG
                var rawLinks = links.Select(link => link.GetNullableHref())
                                    .OfType<string>()
                                    .ToList();
                resolve = rawLinks.Where(url => resolvableMap.Keys.Any(url.Contains))
                                  .ToList();
                foreach (var link in rawLinks.Except(resolve))
                {
                    Log.Debug("Unhandled link: {Link}", link);
                }
                #else
                resolve = links.Select(link => link.GetNullableHref())
                                    .OfType<string>()
                                    .Where(url => resolvableMap.Keys.Any(url.Contains))
                                    .ToList();
                #endif
                var iframes = content.SelectNodesSafe(".//iframe[@class='saint-iframe']")
                                     .Select(iframe => iframe.GetSrc()); //cyberdrop and saint2
                resolve.AddRange(iframes);
                if (resolve.Count != 0)
                {
                    resolveLists.Add(new IndexedContainer<List<string>>(resolve, index));
                }
            }

            var nextPage = soup.SelectSingleNode("//a[@class='pageNav-jump pageNav-jump--next']");
            if (nextPage is null)
            {
                break;
            }

            var nextPageUrl = $"https://simpcity.su{nextPage.GetHref()}";
            Log.Information("Parsing page: {NextPageUrl}", nextPageUrl);
            soup = await Soupify(nextPageUrl, lazyLoadArgs: lazyLoadArgs, delay: 1000, cookies: cookieJar);
        }

        return (images, resolveLists, dirName);
    }

    private Dictionary<string, ParameterizedHtmlParser> CreateResolveMap()
    {
        var bunkr = new BunkrParser(WebDriver, RequestHeaders);
        var jpg5 = new Jpg5Parser(WebDriver, RequestHeaders);
        var resolvableMap = new Dictionary<string, ParameterizedHtmlParser>
        {
            ["bunkrrr.org"] = bunkr,
            ["bunkr."] = bunkr,
            ["gofile.io"] = new GoFileParser(WebDriver, RequestHeaders),
            ["pixeldrain.com"] = new PixelDrainParser(WebDriver, RequestHeaders),
            ["cyberdrop.me"] = new CyberDropParser(WebDriver, RequestHeaders),
            ["jpg4.su"] = jpg5,
            ["jpg5.su"] = jpg5,
            ["coomer.party"] = new CoomerParser(WebDriver, RequestHeaders),
            ["saint2.cr"] = new Saint2Parser(WebDriver, RequestHeaders),
        };

        return resolvableMap;
    }

    private static string VideoResolver(HtmlNode video)
    {
        var src = video.GetNullableSrc();
        if (src is not null)
        {
            return src.Remove("-mobile");
        }

        var source = video.SelectSingleNode(".//source");
        src = source.GetSrc();
        if (!src.Contains("saint2.pk"))
        {
            return src;
        }

        var grandParent = video.ParentNode.ParentNode;
        var downloadLink = grandParent.SelectSingleNode(".//a[@class='plyr__controls__item plyr__control']").GetHref();
        var id = downloadLink.Split("/d/")[^1];
        return $"https://simp2.saint2.pk/api/download.php?file={id}";
    }

    private Task<RipInfo> ReTryParse(string link, ParameterizedHtmlParser parser)
    {
        WebDriver.RegenerateDriver();
        // Driver.Url = link;
        // var cookieJar = Driver.GetCookieJar();
        // foreach(var cookie in cookies)
        // {
        //     cookieJar.AddCookie(cookie);
        // }

        // Parse w/link will internally reload the page
        return parser.Parse(link);
    }

    private class Cache
    {
        public string Url { get; set; } = null!;
        public string DirName { get; set; } = null!;
        public List<IndexedContainer<List<string>>> ResolveLists { get; set; } = null!;
        public List<StringImageLinkWrapper> Images { get; set; } = null!;
    }
}