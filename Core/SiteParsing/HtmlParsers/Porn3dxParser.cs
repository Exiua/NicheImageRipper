using Core.DataStructures;
using Core.Enums;
using Core.Exceptions;
using Core.ExtensionMethods;
using OpenQA.Selenium;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class Porn3dxParser : HtmlParser
{
    public Porn3dxParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for porn3dx.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        const int maxRetries = 4;
        var cookie = Config.Cookies["Porn3dx"];
        var cookieJar = Driver.Manage().Cookies;
        cookieJar.DeleteCookieNamed("porn3dx_session");
        cookieJar.AddCookie(new Cookie("porn3dx_session", cookie));
        var lazyLoadArgs = new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250,
            ScrollPauseTime = 1000
        };
        var soup = await Soupify(lazyLoadArgs: lazyLoadArgs);
        var dirName = soup.SelectSingleNode("//div[@class='items-center self-center text-sm font-bold leading-none text-white ']")
                            .InnerText;
        var origUrl = CurrentUrl;
        var posts = new List<string>();
        var id = 0;
        if (!await WaitForElement("//a[@id='gallery-0']", timeout: 50))
        {
            throw new RipperException("Element could not be found");
        }
    
        while (true)
        {
            var post = Driver.TryFindElement(By.Id($"gallery-{id}"));
            if (post is null)
            {
                break;
            }
    
            posts.Add(post.GetHref());
            id++;
        }
        
        var images = new List<StringImageLinkWrapper>();
        foreach (var (i, post) in posts.Enumerate())
        {
            var contentFound = false;
            Log.Information("Parsing post {i} of {totalPosts}", i + 1, posts.Count);
            while (!contentFound)
            {
                CurrentUrl = post;
                await Task.Delay(100);
                var iframes = Driver.FindElements(By.XPath("//main[@id='postView']//iframe"));
                var pictures = Driver.FindElements(By.XPath("//picture"));
                while (iframes?.Count == 0 && pictures?.Count == 0)
                {
                    await Task.Delay(5000);
                    if (CurrentUrl == origUrl)
                    {
                        var ad = Driver.TryFindElement(By.XPath("//div[@class='ex-over-top ex-opened']//div[@class='ex-over-btn']"));
                        ad?.Click();
                    }
    
                    CleanTabs("porn3dx");
                    iframes = Driver.FindElements(By.XPath("//main[@id='postView']//iframe"));
                    pictures = Driver.FindElements(By.XPath("//picture"));
                }
    
                if (iframes?.Count != 0)
                {
                    foreach (var iframe in iframes!)
                    {
                        var iframeUrl = iframe.GetSrc();
                        if (!iframeUrl.Contains("iframe.mediadelivery.net"))
                        {
                            continue;
                        }
    
                        contentFound = true;
                        Driver.SwitchTo().Frame(iframe);
                        string url = null!;
                        var maxQuality = 0;
                        for (var _ = 0; _ < maxRetries; _++)
                        {
                            var source = Driver.TryFindElement(By.XPath("//video/source")) ?? Driver.FindElement(By.XPath("//video"));
                            url = source.GetSrc();
                            if (url.StartsWith("blob:") || url == "")
                            {
                                await Task.Delay(500);
                                continue;
                            }
                            var qualities = Driver.FindElements(By.XPath("//button[@data-plyr='quality']"));
                            maxQuality = qualities.Select(quality => int.Parse(quality.GetDomAttribute("value")))
                                                    .Prepend(0)
                                                    .Max();
                            break;
                        }
    
                        images.Add($"{url}{{{maxQuality}}}{iframeUrl}");
                        Driver.SwitchTo().DefaultContent();
                    }
                }
    
                if (pictures?.Count != 0)
                {
                    foreach (var picture in pictures!)
                    {
                        contentFound = true;
                        var imgs = picture.FindElements(By.XPath(".//img"));
                        images.AddRange(imgs.Select(img => img.GetSrc())
                                            .Where(url => url.Contains("m.porn3dx.com") && !url.Contains("avatar") 
                                                    && !url.Contains("thumb"))
                                            .Select(url => (StringImageLinkWrapper)url));
                    }
                }
            }
        }
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
