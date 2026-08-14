using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.SiteParsing.HtmlParsers.SiteSpecific;
public class BabeCentrumParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "babecentrum";
    public static string[] SupportedUrls => ["https://www.babecentrum.com/"];

    public BabeCentrumParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<BabeCentrumParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for babecentrum.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='pageHeading']").SelectNodesOrThrow(".//cufontext").Select(w => w.InnerText).Join(" ").Trim();
        var images = soup.SelectSingleNodeOrThrow("//table").SelectNodesOrThrow(".//img").Select(img => Protocol + img.GetAttributeValue("src", "").Remove("tn_")).Select(dummy => (StringFileLinkWrapper)dummy).ToList();
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}