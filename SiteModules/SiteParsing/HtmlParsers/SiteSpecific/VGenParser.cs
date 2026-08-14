using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using NicheImageRipper.Core.Exceptions;
using NicheImageRipper.Core.SiteParsing.HtmlParsers;
using OpenQA.Selenium;
using HtmlAgilityPack;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.SiteParsing.HtmlParsers.SiteSpecific;
public class VGenParser : HtmlParser
{
    public VGenParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses  the HTML for site and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var dirName = Driver.FindElement(By.XPath("//h1[@class='profileDisplayName']/span")).Text;
        var images = new List<StringFileLinkWrapper>();
        var container = Driver.FindElement(By.XPath("//div[@class='ReactVirtualized__Masonry__innerScrollContainer']"));
        var children = container.FindElements(By.XPath("./div"));
        foreach (var child in children)
        {
            var clickZone = child.FindElement(By.XPath(".//div[@class='showcaseContainer']"));
            clickZone.Click();
            await Sleep(250, cancellationToken: cancellationToken);
            var containerName = await WaitForElement("//div[@class='galleryContainer']", cancellationToken: cancellationToken);
            while (containerName is null)
            {
                await Sleep(250, cancellationToken: cancellationToken);
                containerName = await WaitForElement("//div[@class='galleryContainer']", cancellationToken: cancellationToken);
            }

            var imageContainer = Driver.FindElement(By.XPath("//div[@class='galleryContainer']"));
            var imgs = imageContainer.FindElements(By.XPath("./img"));
            foreach (var img in imgs)
            {
                var src = img.GetAttribute("src");
                if (src is null)
                {
                    Logger.Warning("Image src is null, skipping");
                    continue;
                }

                images.Add(src);
            }

            var closeButton = Driver.FindElement(By.XPath("//div[@class='sc-389b16ca-0 kmkUyu sc-c6245413-0']//svg"));
            closeButton.Click();
            await Sleep(250, cancellationToken: cancellationToken);
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}