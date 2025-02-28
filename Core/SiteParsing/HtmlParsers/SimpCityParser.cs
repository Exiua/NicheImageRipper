using System.Collections.Frozen;
using System.Collections.ObjectModel;
using Core.DataStructures;
using Core.Enums;
using Core.Exceptions;
using Core.ExtensionMethods;
using Core.Utility;
using HtmlAgilityPack;
using OpenQA.Selenium;
using Serilog;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class SimpCityParser : HtmlParser
{
    public SimpCityParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for simpcity.su and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        if (CurrentUrl.Contains("//www."))
        {
            CurrentUrl = CurrentUrl.Replace("//www.", "//");
        }
        Log.Debug("Getting user cookie");
        var cookieValue = Config.Cookies["SimpCity"];
        Log.Debug("Adding cookie to driver");
        Driver.AddCookie("dontlikebots_user", cookieValue);
        Log.Debug("Solving captcha and parsing page");
        var userCookie = new Dictionary<string, string>
        {
            ["name"] = "dontlikebots_user",
            ["value"] = cookieValue
        };
        var soup = await SolveParseAddCookies(cookies: [ userCookie ]);
        var cookieJar = Driver.GetCookieJar();
        Log.Debug("Saving reference to cookies");
        var cookies = cookieJar.AllCookies;
        Log.Debug("Creating resolvable url map");
        var resolvableMap = new Dictionary<string, Func<string, Task<RipInfo>>>
        {
            ["bunkrrr.org"] = BunkrParse,
            ["bunkr."] = BunkrParse,
            ["gofile.io"] = GoFileParse,
            ["pixeldrain.com"] = PixelDrainParse,
            ["cyberdrop.me"] = CyberDropParse,
            ["jpg4.su"] = Jpg5Parse,
            ["jpg5.su"] = Jpg5Parse,
            ["coomer.party"] = CoomerParse,
        }.ToFrozenDictionary();
        
        Log.Debug("Parsing page");
        var dirName = soup.SelectSingleNode("//h1[@class='p-title-value']").InnerText;
        Log.Debug("Directory name: {DirName}", dirName);
        var images = new List<StringImageLinkWrapper>();
    
        #region Parse Posts
        
        var lazyLoadArgs = new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250,
            ScrollPauseTime = 1000
        };
        var resolveLists = new List<(List<string>, int)>();
        while (true)
        {
            if (Driver.Title == "SimpCity - Rate Limit")
            {
                Log.Warning("Rate limited, waiting 30 seconds");
                await Task.Delay(30000);
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
                    resolveLists.Add((resolve, index));
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
    
        #endregion
        
        #region Resolve Links
    
        var offset = 0;
        var resolved = 1;
        var total = resolveLists.Sum(list => list.Item1.Count);
        foreach (var (resolveList, i) in resolveLists)
        {
            var index = i + offset;
            foreach (var link in resolveList)
            {
                Log.Information("Resolving link {Resolved} of {Total}: {Link}", resolved, total, link);
                resolved++;
                Func<string, Task<RipInfo>>? parser = null;
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
                    info = await parser(link);
                }
                catch (WebDriverTimeoutException)
                {
                    Log.Warning("WebDriver unresponsive, retrying");
                    // Assume driver is dead and unreachable
                    // TODO: Find a better way to handle this
                    info = await ReTryParse(link, parser, cookies);
                }
                catch (WebDriverException e) when (e.Message.Contains("The HTTP request to the remote WebDriver server for URL"))
                {
                    Log.Warning("WebDriver unresponsive, retrying");
                    // Assume driver is dead and unreachable
                    // TODO: Find a better way to handle this
                    info = await ReTryParse(link, parser, cookies);
                }
                
                images.InsertRange(index, info.Urls.ToStringImageLinks());
                offset += info.Urls.Count;
                await Task.Delay(125);
            }
        }
    
        #endregion
    
        return new RipInfo(images, dirName, FilenameScheme);
    
        string VideoResolver(HtmlNode video)
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
    
        Task<RipInfo> ReTryParse(string link, Func<string, Task<RipInfo>> parser, ReadOnlyCollection<Cookie> cookies)
        {
            WebDriver.RegenerateDriver(false);
            cookieJar = Driver.GetCookieJar();
            foreach(var cookie in cookies)
            {
                cookieJar.AddCookie(cookie);
            }
            return parser(link);
        }
    }
}
