

using OpenQA.Selenium;
using Sdk.Common.ExtensionMethods;
using Sdk.DataStructures;
using Sdk.Enums;
using Sdk.Exceptions;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.Thothub;
public class ThothubParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "thothub";
    public static string[] SupportedUrls => ["https://thothub.lol/"];

    public ThothubParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<ThothubParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for thothub.lol and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        const string sessionCookieName = "PHPSESSID";
        var cookieJar = Driver.GetCookieJar();
        var cookie = cookieJar.GetCookieNamed(sessionCookieName);
        if (cookie is not null)
        {
            cookieJar.DeleteCookie(cookie);
        }

        var cookies = Config.Cookies.GetValueOrDefault(ParserName, []);
        if (cookies.Length == 0)
        {
            throw new MissingCookieException(sessionCookieName, ParserName);
        }
        
        var cookieValue = cookies[0];
        cookieJar.AddCookie(new Cookie(sessionCookieName, cookieValue));
        Driver.Refresh();
        var lazyLoadArgs = new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 625,
            ScrollPauseTime = 1
        };
        var soup = await Soupify(lazyLoadArgs: lazyLoadArgs, delay: 1000, cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//div[@class='headline']").SelectSingleNodeOrThrow(".//h1").InnerText;
        List<StringFileLinkWrapper> images;
        if (CurrentUrl.Contains("/videos/"))
        {
            var vid = soup.SelectSingleNodeOrThrow("//video[@class='fp-engine']").GetSrc();
            if (string.IsNullOrEmpty(vid))
            {
                vid = soup.SelectSingleNodeOrThrow("//div[@class='no-player']").SelectSingleNodeOrThrow(".//img").GetSrc();
            }

            images = [vid];
        }
        else
        {
            while (true)
            {
                var posts = soup.SelectSingleNodeOrThrow("//div[@class='images']").SelectNodesOrThrow(".//img").Select(img => img.GetSrc().Replace("/main/200x150/", "/sources/")).ToArray();
                if (posts.Any(p => p.Contains("data:")))
                {
                    await Sleep(1000, cancellationToken: cancellationToken);
                    ScrollToTop();
                    soup = await Soupify(lazyLoadArgs: lazyLoadArgs, cancellationToken: cancellationToken);
                    continue;
                }

                images = posts.ToStringFileLinkWrapperList();
                break;
            }
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}