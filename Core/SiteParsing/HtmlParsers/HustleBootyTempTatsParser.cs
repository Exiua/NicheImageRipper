using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using OpenQA.Selenium;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class HustleBootyTempTatsParser : HtmlParser
{
    public HustleBootyTempTatsParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for hustlebootytemptats.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var pauseButton = Driver.TryFindElement(By.XPath("//div[@class='galleria-playback-button pause']"));
        pauseButton?.Click();
        var soup = await Soupify(delay: 1000);
        var dirName = soup.SelectSingleNode("//h1[@class='zox-post-title left entry-title']").InnerText;
        var imagesNode = soup.SelectNodes("//div[@class='galleria-thumbnails']//img");
        List<StringImageLinkWrapper> images;
        if (imagesNode is not null)
        {
            images = imagesNode.Select(img => img.GetSrc().Remove("/cache").Split("-nggid")[0])
                                .ToStringImageLinkWrapperList();
        }
        else
        {
            var nextButton = Driver.TryFindElement(By.XPath("//div[@class='galleria-image-nav-right']"));
            if (nextButton is not null)
            {
                images = [];
                var seen = new HashSet<string>();
                var newImages = true;
                while (newImages)
                {
                    var imageNodes = Driver.FindElements(By.XPath("//div[@class='galleria-image']//img"));
                    newImages = false;
                    foreach (var imageNode in imageNodes)
                    {
                        var src = imageNode.GetSrc();
                        if (!seen.Add(src))
                        {
                            continue;
                        }
    
                        newImages = true;
                        images.Add(src);
                    }
    
                    nextButton.Click();
                    await Task.Delay(1000);
                }
            }
            else
            {
                var iframe = soup.SelectSingleNode("//iframe");
                var iframeUrl = iframe.GetSrc();
                images = [ iframeUrl ]; // YouTube video (probably)
            }
        }
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
