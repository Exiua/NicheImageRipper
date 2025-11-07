using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using OpenQA.Selenium;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class VGenParser : HtmlParser
{
    public VGenParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for site and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var dirName = Driver.FindElement(By.XPath("//h1[@class='profileDisplayName']/span")).Text;
        var images = new List<StringImageLinkWrapper>();
        var container = Driver.FindElement(By.XPath("//div[@class='ReactVirtualized__Masonry__innerScrollContainer']"));
        var children = container.FindElements(By.XPath("./div"));
        foreach (var child in children)
        {
            var clickZone = child.FindElement(By.XPath(".//div[@class='showcaseContainer']"));
            clickZone.Click();
            await Sleep(250);
            var containerName = await WaitForElement("//div[@class='galleryContainer']");
            while (containerName is null)
            {
                await Sleep(250);
                containerName = await WaitForElement("//div[@class='galleryContainer']");
            }
            
            var imageContainer = Driver.FindElement(By.XPath("//div[@class='galleryContainer']"));
            var imgs = imageContainer.FindElements(By.XPath("./img"));
            foreach (var img in imgs)
            {
                var src = img.GetAttribute("src");
                if (src is null)
                {
                    Log.Warning("Image src is null, skipping");
                    continue;
                }
                
                images.Add(src);
            }
            
            var closeButton = Driver.FindElement(By.XPath("//div[@class='sc-389b16ca-0 kmkUyu sc-c6245413-0']//svg"));
            closeButton.Click();
            await Sleep(250);
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}