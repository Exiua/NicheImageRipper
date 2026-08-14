using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.SiteParsing.HtmlParsers.SiteSpecific;
public class Zero6SeParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "06se";
    public static string[] SupportedUrls => ["https://www.06se.com/"];

    public Zero6SeParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<Zero6SeParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for 06se.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        // var readMoreButton = Driver.TryFindElement(By.XPath("//div[@class='read-more']/a"));
        // readMoreButton?.Click();
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='article-title']/a").InnerText;
        var images = soup.SelectSingleNodeOrThrow("//div[@class='article-content']").SelectNodesOrThrow(".//img").Select(GetUrl).OfType<string>().ToStringImageLinkWrapperList();
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }

    private static string? GetUrl(HtmlNode img)
    {
        var src = img.GetNullableAttributeValue("data-src");
        if (src is not null)
        {
            return src;
        }

        src = img.GetNullableSrc();
        return string.IsNullOrWhiteSpace(src) ? null : src;
    }
}