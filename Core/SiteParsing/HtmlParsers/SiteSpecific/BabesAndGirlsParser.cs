using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;

public class BabesAndGirlsParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "babesandgirls";

    public BabesAndGirlsParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<BabesAndGirlsParser>(filenameScheme))
    {
    }
    
    /// <summary>
    ///     Parses  the HTML for babesandgirls.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='title']")
                          .InnerText;
        var images = soup.SelectSingleNodeOrThrow("//div[@class='block-post album-item']")
                         .SelectNodesOrThrow(".//a[@class='item-post']")
                         .Select(img => Protocol + img.SelectSingleNodeOrThrow(".//img").GetSrc().Remove("tn_"))
                         .ToStringImageLinkWrapperList();

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}