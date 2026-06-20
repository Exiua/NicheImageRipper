using Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Exceptions;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using OpenQA.Selenium;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers;

public class KnitParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "knit";

    public KnitParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<KnitParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for xx.knit.bid and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var lazyLoadArgs = new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250,
            ScrollPauseTime = 2500
        };
        
        var agreeButton = Driver.TryFindElement(By.XPath("//button[@id='agree-over18']"));
        if (agreeButton is not null)
        {
            Driver.Click(agreeButton);
        }
        
        var retry = 0;
        while (true)
        {
            await LazyLoad(lazyLoadArgs);
            var scrollHeight = Driver.GetScrollHeight();
            if (scrollHeight <= 650)
            {
                Logger.Debug("Page reset");
                CleanTabs("xx.knit.bid");
                retry++;
                if (retry > 4)
                {
                    throw new RipperException("Page reset too many times");
                }
                continue;
            }
            
            var loadMoreButton = Driver.TryFindElement(By.XPath("//div[@class='ias_trigger']"));
            if (loadMoreButton is null)
            {
                Logger.Debug("No more images to load");
                break;
            }
            
            loadMoreButton.Click();
        }

        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='focusbox-title']").InnerText;
        var baseUrl = CurrentUrl.Split("/").Take(6).Join("/");
        var images = new List<StringImageLinkWrapper>();
        var page = 1;
        while(true)
        {
            Logger.Information("Parsing page {Page}", page);
            var imgs = soup.SelectSingleNodeOrThrow("//div[@class='image-container']")
                             .SelectNodesOrThrow("./p")
                             .Select(p => "https://xx-media.knit.bid" + p.SelectSingleNodeOrThrow("./img").GetSrc())
                             .ToStringImageLinkWrapperList();
            var imageGallery = soup.SelectSingleNode("//article[@id='image-gallery']");
            if (imageGallery is not null)
            {
                var videos = imageGallery.SelectSingleNodeOrThrow(".//div[@class='wrapper']")
                                         .SelectNodesSafe(".//source")
                                         .Select(source => source.GetSrc())
                                         .ToStringImageLinks();
                imgs.AddRange(videos);
            }
            
            images.AddRange(imgs);
            page++;
            
            var nextPageButton = soup.SelectSingleNode("//li[@class='next-page']");
            if (nextPageButton is not null)
            {
                soup = await Soupify($"{baseUrl}/page/{page}", lazyLoadArgs: lazyLoadArgs);
            }
            else
            {
                break;
            }
        }
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
