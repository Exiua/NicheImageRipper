using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.SiteParsing.HtmlParsers.SiteSpecific;
public class NudeBirdParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "nudebird";
    public static string[] SupportedUrls => ["https://nudebird.biz/"];

    public NudeBirdParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<NudeBirdParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the HTML for nudebird.biz and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='title single-title entry-title']").InnerText;
        var images = soup.SelectNodesOrThrow("//a[@class='fancybox-thumb']").Select(img => img.GetHref()).ToStringImageLinkWrapperList();
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}