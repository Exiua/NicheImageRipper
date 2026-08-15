using System.Text.RegularExpressions;
using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using NicheImageRipper.Core.Exceptions;
using NicheImageRipper.Core.SiteParsing.HtmlParsers;
using OpenQA.Selenium;
using HtmlAgilityPack;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.SiteParsing.HtmlParsers.SiteSpecific;

public partial class NewgroundsParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "newgrounds";
    public static string[] SupportedUrls { get; } = ["https://newgrounds.com/"];

    public NewgroundsParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders,
                            FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager,
        requestHeaders, IHtmlParser.GetFilenameScheme<NewgroundsParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the HTML for rule34.xxx and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        const string cookieName = "vmk1du5I8m";
        var lazyLoadArgs = new LazyLoadArgs
        {
            ScrollBy = true
        };
        var cookies = Config.Cookies.GetValueOrDefault(ParserName, []);
        if (cookies.Length == 0)
        {
            throw new MissingCookieException(cookieName, ParserName);
        }
        
        var cookie = cookies[0];
        var cookieJar = Driver.Manage().Cookies;
        cookieJar.DeleteAllCookies();
        cookieJar.AddCookie(new Cookie(cookieName, cookie));
        var baseUri = CurrentUrl.Split("/")[..3];
        var baseUriString = string.Join("/", baseUri);
        var soup = await Soupify(baseUriString, cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//a[@class='user-link']").InnerText.Trim();
        var headerButtons = soup.SelectSingleNodeOrThrow("//div[@class='user-header-buttons']")
                                .SelectNodesOrThrow(".//a");
        var hasMovies = false;
        var hasArt = false;
        foreach (var button in headerButtons)
        {
            var href = button.GetHref();
            switch (href)
            {
                case "/movies":
                    hasMovies = true;
                    break;
                case "/art":
                    hasArt = true;
                    break;
            }
        }

        var images = new List<StringFileLinkWrapper>();
        if (hasArt)
        {
            soup = await Soupify($"{baseUriString}/art", lazyLoadArgs: lazyLoadArgs, cancellationToken: cancellationToken);
            var posts = GetPosts(soup, false);
            var numPosts = posts.Count;
            foreach (var (i, post) in posts.Enumerate())
            {
                Logger.Information("Parsing Art Post {i}/{numPosts}", i + 1, numPosts);
                soup = await Soupify(post, lazyLoadArgs: lazyLoadArgs, delay: 100, cancellationToken: cancellationToken);
                var artImages = soup.SelectSingleNode("//div[contains(@class, 'art-images')]");
                if (artImages is not null)
                {
                    var links = artImages.SelectNodesOrThrow(".//img")
                                         .Select(img => (StringFileLinkWrapper)img.GetSrc());
                    images.AddRange(links);
                }
                else
                {
                    var artViewGallery = soup.SelectSingleNode("//div[@class='art-view-gallery']");
                    if (artViewGallery is not null)
                    {
                        var seen = new HashSet<string>();
                        while (true)
                        {
                            artViewGallery = soup.SelectSingleNodeOrThrow("//div[@class='art-view-gallery']");
                            var container =
                                artViewGallery.SelectSingleNodeOrThrow(
                                    ".//div[@class='ng-img-container-sync relative']");
                            var anchor = container.SelectSingleNodeOrThrow(".//a");
                            var link = anchor.GetHref();
                            if (!seen.Add(link))
                            {
                                break;
                            }

                            images.Add(link);
                            var nextBtn = Driver.FindElement(By.XPath("//a[@class='gallery-nav right']"));
                            try
                            {
                                nextBtn.Click();
                            }
                            catch (ElementClickInterceptedException)
                            {
                                var blackoutZone =
                                    Driver.FindElement(By.XPath("(//div[@class='blackout-bookend'])[3]"));
                                blackoutZone.Click();
                                nextBtn.Click();
                            }

                            await Sleep(500, cancellationToken);
                            soup = await Soupify(cancellationToken: cancellationToken);
                        }
                    }
                    else
                    {
                        var img = soup.SelectSingleNodeOrThrow("//div[@class='image']")
                                      .SelectSingleNodeOrThrow(".//img");
                        images.Add(img.GetSrc());
                    }
                }
            }
        }

        if (hasMovies)
        {
            soup = await Soupify($"{baseUriString}/movies", lazyLoadArgs: lazyLoadArgs, cancellationToken: cancellationToken);
            var posts = GetPosts(soup, true);
            var numPosts = posts.Count;
            foreach (var (i, post) in posts.Enumerate())
            {
                await Sleep(100, cancellationToken);
                Logger.Information("Parsing Movie Post {i}/{numPosts}", i + 1, numPosts);
                CurrentUrl = post;
                await LazyLoad(lazyLoadArgs, cancellationToken);
                var videoStart = Driver.TryFindElement(By.XPath("//div[@class='video-barrier']/child::*[2]"));
                if (videoStart is not null)
                {
                    try
                    {
                        videoStart.Click();
                    }
                    catch (ElementClickInterceptedException)
                    {
                        var blackoutZone = Driver.FindElement(By.XPath("(//div[@class='blackout-bookend'])[3]"));
                        blackoutZone.Click();
                        videoStart.Click();
                    }

                    await Sleep(500, cancellationToken);
                    var optionsBtn = Driver.FindElement(By.XPath("//button[@title='Display Options']"));
                    optionsBtn.Click();
                    var highestRes =
                        Driver.TryFindElement(By.XPath("//div[@class='ng-option-select']/child::*[2]/child::*[1]"));
                    if (highestRes is not null)
                    {
                        var classes = highestRes.GetDomAttribute("class")!;
                        if (!classes.Contains("selected"))
                        {
                            highestRes.Click();
                        }
                    }

                    soup = await Soupify(cancellationToken: cancellationToken);
                    var video = soup.SelectSingleNodeOrThrow("//video");
                    var videoUrl = video.SelectSingleNodeOrThrow(".//source").GetSrc();
                    while (videoUrl.StartsWith("data:"))
                    {
                        await Sleep(1000, cancellationToken);
                        soup = await Soupify(cancellationToken: cancellationToken);
                        video = soup.SelectSingleNodeOrThrow("//video");
                        videoUrl = video.SelectSingleNodeOrThrow(".//source").GetSrc();
                    }

                    images.Add(videoUrl);
                }
                else
                {
                    soup = await Soupify(cancellationToken: cancellationToken);
                    // Assumes the video is an emulated flash video
                    var script = soup.SelectSingleNodeOrThrow("//div[@class='body-guts top']")
                                     .SelectNodesOrThrow(".//script")[1]
                                     .InnerText;
                    var videoUrl = NewgroundsRegex().Match(script).Value;
                    images.Add(videoUrl);
                }
            }
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);

        // ReSharper disable once VariableHidesOuterVariable
        List<string> GetPosts(HtmlNode soup, bool movies)
        {
            var posts = new List<string>();
            var postYears = soup.SelectSingleNodeOrThrow("//div[@class='userpage-browse-content']//div")
                                .SelectNodesOrThrow("./div");
            foreach (var postYear in postYears)
            {
                var postLinks = postYear.SelectNodesOrThrow(!movies
                    ? ".//div[@class='span-1 align-center']"
                    : ".//div[@class='portalsubmission-cell']");

                var postLinksList = postLinks.Select(post => post.SelectSingleNodeOrThrow(".//a").GetHref());
                posts.AddRange(postLinksList);
            }

            return posts;
        }
    }

    [GeneratedRegex(@"swf: ?""([^""]+)""")]
    private static partial Regex NewgroundsRegex();
}