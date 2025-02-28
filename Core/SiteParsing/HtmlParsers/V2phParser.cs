using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using OpenQA.Selenium;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class V2phParser : HtmlParser
{
    public V2phParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for v2ph.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        // TODO: Work on bypassing cloudflare
        // The parser works, but when changing pages, the cf_clearance cookie gets refreshed in a way that causes issues
        var cookies = Config.Custom["V2PH"];
        var frontendCookie = cookies["frontend"];
        var frontendRmtCookie = cookies["frontend-rmt"];
        var cfClearanceCookie = cookies["cf_clearance"];
        var cookieJar = Driver.GetCookieJar();
        cookieJar.SetCookie("cf_clearance", cfClearanceCookie);
        Driver.Refresh();
        cookieJar.SetCookie("frontend", frontendCookie);
        cookieJar.SetCookie("frontend-rmt", frontendRmtCookie);
        Driver.Refresh();
        var lazyLoadArgs = new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250,
            ScrollPauseTime = 750
        };
        var soup = await Soupify(lazyLoadArgs: lazyLoadArgs, delay: 1000);
        var dirName = soup.SelectSingleNode("//h1[@class='h5 text-center mb-3']")
                            .InnerText;
        var numPages = int.Parse(soup.SelectSingleNode("//dl[@class='row mb-0']")
                                    .SelectNodes(".//dd")[^1]
                                    .InnerText) / 10 + 1;
        var baseLink = CurrentUrl.Split("?")[0];
        var images = new List<StringImageLinkWrapper>();
        var parseComplete = false;
        for (var i = 0; i < numPages; i++)
        {
            if (i != 0)
            {
                var nextPage = $"{baseLink}?page={i + 1}";
                CurrentUrl = nextPage;
                var footer = Driver.TryFindElement(By.Id("footer-text"));
                if (footer is not null && footer.Text.Contains("Cloudflare"))
                {
                    cookieJar.SetCookie("cf_clearance", cfClearanceCookie);
                    Driver.Refresh();
                }
                soup = await Soupify(lazyLoadArgs: lazyLoadArgs, delay: 1000);
                Console.ReadLine();
            }
    
            List<StringImageLinkWrapper> imageList;
            while (true)
            {
                imageList = soup.SelectSingleNode("//div[@class='photos-list text-center']")
                                .SelectNodes(".//div[@class='album-photo my-2']")
                                .Select(img => img.SelectSingleNode(".//img").GetSrc())
                                .ToStringImageLinkWrapperList();
                if (imageList.Count == 0)
                {
                    parseComplete = true;
                    break;
                }
                
                if (imageList.All(img => !img.Contains("data:image/gif;base64")))
                {
                    break;
                }
    
                //Driver.FindElement(By.TagName("body")).SendKeys(Keys.Control + Keys.Home);
                ScrollToTop();
                soup = await Soupify(lazyLoadArgs: lazyLoadArgs);
            }
            
            images.AddRange(imageList);
            if (parseComplete)
            {
                break;
            }
        }
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
