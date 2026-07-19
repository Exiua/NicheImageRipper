using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using OpenQA.Selenium;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers;

public class V2phParser : HtmlParser
{
    public V2phParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses  the HTML for v2ph.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        // TODO: Work on bypassing cloudflare
        // The parser works, but when changing pages, the cf_clearance cookie gets refreshed in a way that causes issues
        var cookies = Config.Custom.V2PH;
        var frontendCookie = cookies.Frontend;
        var frontendRmtCookie = cookies.FrontendRmt;
        var cfClearanceCookie = cookies.CfClearance;
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
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='h5 text-center mb-3']")
                            .InnerText;
        var numPages = int.Parse(soup.SelectSingleNodeOrThrow("//dl[@class='row mb-0']")
                                     .SelectNodesOrThrow(".//dd")[^1]
                                     .InnerText) / 10 + 1;
        var baseLink = CurrentUrl.Split("?")[0];
        var images = new List<StringFileLinkWrapper>();
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
    
            List<StringFileLinkWrapper> imageList;
            while (true)
            {
                imageList = soup.SelectSingleNodeOrThrow("//div[@class='photos-list text-center']")
                                .SelectNodesSafe(".//div[@class='album-photo my-2']")
                                .Select(img => img.SelectSingleNodeOrThrow(".//img").GetSrc())
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
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
