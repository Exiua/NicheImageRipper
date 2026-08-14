using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.SiteParsing.HtmlParsers.SiteSpecific;
public class BabeUniversumParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "babeuniversum";
    public static string[] SupportedUrls => ["https://www.babeuniversum.com/"];

    public BabeUniversumParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<BabeUniversumParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for babeuniversum.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//div[@class='title']").SelectSingleNodeOrThrow(".//h1").InnerText;
        var images = soup.SelectSingleNodeOrThrow("//div[@class='three-column']").SelectNodesOrThrow(".//div[@class='thumbnail']").Select(img => Protocol + img.SelectSingleNodeOrThrow(".//img").GetSrc().Remove("tn_")).Select(dummy => (StringFileLinkWrapper)dummy).ToList();
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}