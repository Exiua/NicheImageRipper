using Core.DataStructures;
using Core.Enums;
using Core.Exceptions;
using Core.ExtensionMethods;
using OpenQA.Selenium;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class KnitParser : HtmlParser
{
    public KnitParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for xx.knit.bid and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var lazyLoadArgs = new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250,
            ScrollPauseTime = 1000
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
                Log.Debug("Page reset");
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
                Log.Debug("No more images to load");
                break;
            }
            
            loadMoreButton.Click();
        }
    
        var soup  = await Soupify();    
        var dirName = soup.SelectSingleNode("//h1[@class='focusbox-title']").InnerText;
        var images = soup.SelectSingleNode("//article[@id='img-box']")
            .SelectNodes("./p")
            .Select(p => "https://xx-media.knit.bid" + p.SelectSingleNode("./img").GetSrc())
            .ToStringImageLinkWrapperList();
        var videos = soup.SelectSingleNode("//article[@id='img-box']/div[@class='wrapper']")
            .SelectNodesSafe(".//source")
            .Select(source => source.GetSrc())
            .ToStringImageLinks();
        images.AddRange(videos);
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
