using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using OpenQA.Selenium;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.SimplyCosplay;
public class SimplyCosplayParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "simply-cosplay";
    public static string[] SupportedUrls => ["https://www.simply-cosplay.com/"];

    public SimplyCosplayParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<SimplyCosplayParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for simply-cosplay.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        await Sleep(5000, cancellationToken: cancellationToken);
        var viewButton = Driver.TryFindElement(By.XPath("//button[@class='btn btn-default']"));
        viewButton?.Click();
        var soup = await Soupify(cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='content-headline']").InnerText;
        var imageList = soup.SelectSingleNode("//div[@class='swiper-wrapper']");
        List<StringFileLinkWrapper> images;
        if (imageList is null)
        {
            images = [soup.SelectSingleNodeOrThrow("//div[@class='image-wrapper']//img").GetSrc()];
        }
        else
        {
            images = soup.SelectSingleNodeOrThrow("//section/div[@class='row vertical-gutters']").SelectNodesOrThrow(".//img").Select(url => url.GetAttributeValue("data-src").Remove("thumb_")).ToStringFileLinkWrapperList();
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}