using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.SiteParsing.HtmlParsers.SiteSpecific;
public class BestPrettyGirlParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "bestprettygirl";
    public static string[] SupportedUrls => ["https://bestprettygirl.com/"];

    public BestPrettyGirlParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<BestPrettyGirlParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for bestprettygirl.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='elementor-heading-title elementor-size-large']").InnerText;
        var images = soup.SelectNodesOrThrow("//img[@class='aligncenter size-full']").Select(img => img.GetSrc()).Select(dummy => (StringFileLinkWrapper)dummy).ToList();
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}