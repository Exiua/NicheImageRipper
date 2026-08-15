using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.BabesAndBitches;
public class BabesAndBitchesParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "babesandbitches";
    public static string[] SupportedUrls => ["https://www.babesandbitches.net/"];

    public BabesAndBitchesParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<BabesAndBitchesParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for babesandbitches.net and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@id='title']").InnerText.Split("picture")[0].Trim();
        var images = soup.SelectNodesOrThrow("//a[@class='gallery-thumb']").Select(img => Protocol + img.SelectSingleNodeOrThrow(".//img").GetSrc().Remove("tn_")).ToStringImageLinkWrapperList();
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}