using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using OpenQA.Selenium;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class ThothubParser : HtmlParser
{
    public ThothubParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for thothub.lol and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        const string sessionCookieName = "PHPSESSID";
        var cookieJar = Driver.GetCookieJar();
        var cookie = cookieJar.GetCookieNamed(sessionCookieName);
        if (cookie is not null)
        {
            cookieJar.DeleteCookie(cookie);
        }
        cookieJar.AddCookie(new Cookie(sessionCookieName, Config.Cookies["Thothub"]));
        Driver.Refresh();
        var lazyLoadArgs = new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 625,
            ScrollPauseTime = 1
        };
        var soup = await Soupify(lazyLoadArgs: lazyLoadArgs, delay: 1000);
        var dirName = soup.SelectSingleNode("//div[@class='headline']")
                            .SelectSingleNode(".//h1")
                            .InnerText;
        List<StringImageLinkWrapper> images;
        if (CurrentUrl.Contains("/videos/"))
        {
            var vid = soup.SelectSingleNode("//video[@class='fp-engine']")
                            .GetSrc();
            if (string.IsNullOrEmpty(vid))
            {
                vid = soup.SelectSingleNode("//div[@class='no-player']")
                            .SelectSingleNode(".//img")
                            .GetSrc();
            }
    
            images = [vid];
        }
        else
        {
            while (true)
            {
                var posts = soup.SelectSingleNode("//div[@class='images']")
                                .SelectNodes(".//img")
                                .Select(img => img.GetSrc().Replace("/main/200x150/", "/sources/"))
                                .ToArray();
                if(posts.Any(p => p.Contains("data:")))
                {
                    await Task.Delay(1000);
                    ScrollToTop();
                    soup = await Soupify(lazyLoadArgs: lazyLoadArgs);
                    continue;
                }
                
                images = posts.ToStringImageLinkWrapperList();
                break;
            }
        }
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
