using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.SiteParsing.HtmlParsers.SiteSpecific;
public class GirlsReleasedParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "girlsreleased";
    public static string[] SupportedUrls => ["https://girlsreleased.com/"];

    public GirlsReleasedParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<GirlsReleasedParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for girlsreleased.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(delay: 5000, cancellationToken: cancellationToken);
        var metadata = soup.SelectNodesOrThrow("//a[@class='separate']");
        var siteName = metadata[0].InnerText.Split(".")[0].ToTitle();
        var modelName = metadata[1].InnerText;
        var setName = metadata[2].InnerText;
        var dirName = $"{{{siteName}}} {setName} [{modelName}]";
        var images = soup.SelectSingleNodeOrThrow("//div[@class='images']").SelectNodesOrThrow(".//img").Select(img => img.GetSrc().Replace("/t/", "/i/").Replace("t.imx", "i.imx")).Select(dummy => (StringFileLinkWrapper)dummy).ToList();
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}