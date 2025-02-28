using System.Text.RegularExpressions;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using HtmlAgilityPack;
using OpenQA.Selenium;
using Serilog;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public partial class NewgroundsParser : HtmlParser
{
    public NewgroundsParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for rule34.xxx and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var lazyLoadArgs = new LazyLoadArgs
        {
            ScrollBy = true
        };
        var cookieValue = Config.Cookies["Newgrounds"];
        var cookieJar = Driver.Manage().Cookies;
        cookieJar.DeleteAllCookies();
        cookieJar.AddCookie(new Cookie("vmk1du5I8m", cookieValue));
        var baseUri = CurrentUrl.Split("/")[..3];
        var baseUriString = string.Join("/", baseUri);
        var soup = await Soupify(baseUriString);
        var dirName = soup.SelectSingleNode("//a[@class='user-link']").InnerText.Trim();
        var headerButtons = soup.SelectSingleNode("//div[@class='user-header-buttons']")
                                    .SelectNodes(".//a");
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
        
        var images = new List<StringImageLinkWrapper>();
        if (hasArt)
        {
            soup = await Soupify($"{baseUriString}/art", lazyLoadArgs: lazyLoadArgs);
            var posts = GetPosts(soup, false);
            var numPosts = posts.Count;
            foreach (var (i, post) in posts.Enumerate())
            {
                Log.Information("Parsing Art Post {i}/{numPosts}", i + 1, numPosts);
                soup = await Soupify(post, lazyLoadArgs: lazyLoadArgs, delay: 100);
                var artImages = soup.SelectSingleNode("//div[contains(@class, 'art-images')]");
                if (artImages is not null)
                {
                    var links = artImages.SelectNodes(".//img")
                                            .Select(img => (StringImageLinkWrapper)img.GetSrc());
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
                            artViewGallery = soup.SelectSingleNode("//div[@class='art-view-gallery']");
                            var container =
                                artViewGallery.SelectSingleNode(".//div[@class='ng-img-container-sync relative']");
                            var anchor = container.SelectSingleNode(".//a");
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
                                var blackoutZone = Driver.FindElement(By.XPath("(//div[@class='blackout-bookend'])[3]"));
                                blackoutZone.Click();
                                nextBtn.Click();
                            }
    
                            await Task.Delay(500);
                            soup = await Soupify();
                        }
                    }
                    else
                    {
                        var img = soup.SelectSingleNode("//div[@class='image']")
                                        .SelectSingleNode(".//img");
                        images.Add(img.GetSrc());
                    }
                }
            }
        }
    
        if (hasMovies)
        {
            soup = await Soupify($"{baseUriString}/movies", lazyLoadArgs: lazyLoadArgs);
            var posts = GetPosts(soup, true);
            var numPosts = posts.Count;
            foreach (var (i, post) in posts.Enumerate())
            {
                await Task.Delay(100);
                Log.Information("Parsing Movie Post {i}/{numPosts}", i + 1, numPosts);
                CurrentUrl = post;
                await LazyLoad(lazyLoadArgs);
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
                    await Task.Delay(500);
                    var optionsBtn = Driver.FindElement(By.XPath("//button[@title='Display Options']"));
                    optionsBtn.Click();
                    var highestRes = Driver.TryFindElement(By.XPath("//div[@class='ng-option-select']/child::*[2]/child::*[1]"));
                    if (highestRes is not null)
                    {
                        var classes = highestRes.GetDomAttribute("class");
                        if (!classes.Contains("selected"))
                        {
                            highestRes.Click();
                        }
                    }
                    soup = await Soupify();
                    var video = soup.SelectSingleNode("//video");
                    var videoUrl = video.SelectSingleNode(".//source").GetSrc();
                    while (videoUrl.StartsWith("data:"))
                    {
                        await Task.Delay(1000);
                        soup = await Soupify();
                        video = soup.SelectSingleNode("//video");
                        videoUrl = video.SelectSingleNode(".//source").GetSrc();
                    }
                    images.Add(videoUrl);
                }
                else
                {
                    soup = await Soupify();
                    // Assumes the video is an emulated flash video
                    var script = soup.SelectSingleNode("//div[@class='body-guts top']")
                                        .SelectNodes(".//script")[1]
                                        .InnerText;
                    var videoUrl = NewgroundsRegex().Match(script).Value;
                    images.Add(videoUrl);
                }
            }
        }
    
        return new RipInfo(images, dirName, FilenameScheme);
    
        // ReSharper disable once VariableHidesOuterVariable
        List<string> GetPosts(HtmlNode soup, bool movies)
        {
            var posts = new List<string>();
            var postYears = soup.SelectSingleNode("//div[@class='userpage-browse-content']//div")
                                .SelectNodes("./div");
            foreach (var postYear in postYears)
            {
                var postLinks = postYear.SelectNodes(!movies 
                    ? ".//div[@class='span-1 align-center']" 
                    : ".//div[@class='portalsubmission-cell']");
    
                var postLinksList = postLinks.Select(post => post.SelectSingleNode(".//a").GetHref());
                posts.AddRange(postLinksList);
            }
    
            return posts;
        }
    }
    
    [GeneratedRegex(@"swf: ?""([^""]+)""")]
    private static partial Regex NewgroundsRegex();
}
