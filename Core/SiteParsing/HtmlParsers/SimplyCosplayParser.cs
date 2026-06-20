using Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using OpenQA.Selenium;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers;

public class SimplyCosplayParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "simply-cosplay";

    public SimplyCosplayParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<SimplyCosplayParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for simply-cosplay.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        await Sleep(5000);
        var viewButton = Driver.TryFindElement(By.XPath("//button[@class='btn btn-default']"));
        viewButton?.Click();
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='content-headline']").InnerText;
        var imageList = soup.SelectSingleNode("//div[@class='swiper-wrapper']");
        List<StringImageLinkWrapper> images;
        if (imageList is null)
        {
            images = [soup.SelectSingleNodeOrThrow("//div[@class='image-wrapper']//img")
                            .GetSrc()];
        }
        else
        {
            images = soup.SelectSingleNodeOrThrow("//section/div[@class='row vertical-gutters']")
                            .SelectNodesOrThrow(".//img")
                            .Select(url => url.GetAttributeValue("data-src").Remove("thumb_"))
                            .ToStringImageLinkWrapperList();
        }
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
