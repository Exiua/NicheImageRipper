using NicheImageRipper.Sdk.Common.ExtensionMethods;
using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.BabesAndGirls;

public class BabesAndGirlsParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "babesandgirls";
    public static string[] SupportedUrls => ["https://www.babesandgirls.com/"];

    public BabesAndGirlsParser(WebDriver driver, Dictionary<string, string> requestHeaders,
                               FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders,
        IHtmlParser.GetFilenameScheme<BabesAndGirlsParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for babesandgirls.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='title']").InnerText;
        var images = soup.SelectSingleNodeOrThrow("//div[@class='block-post album-item']")
                         .SelectNodesOrThrow(".//a[@class='item-post']")
                         .Select(img => Protocol + img.SelectSingleNodeOrThrow(".//img").GetSrc().Remove("tn_"))
                         .ToStringFileLinkWrapperList();
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}