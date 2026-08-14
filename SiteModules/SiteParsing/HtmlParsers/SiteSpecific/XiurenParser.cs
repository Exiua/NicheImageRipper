using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.SiteParsing.HtmlParsers.SiteSpecific;
public class XiurenParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "xiuren";
    public static string[] SupportedUrls => ["https://xiuren.biz/"];

    public XiurenParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<XiurenParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for xiuren.biz and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='jeg_post_title']").InnerText;
        var images = soup.SelectSingleNodeOrThrow("//div[@class='content-inner ']").SelectNodesOrThrow(".//a").Select(img => img.GetHref()).ToStringImageLinkWrapperList();
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}