using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.SiteParsing.HtmlParsers.SiteSpecific;
public class NovoPornParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "novoporn";
    public static string[] SupportedUrls => ["https://www.novoporn.com/"];

    public NovoPornParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<NovoPornParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the HTML for novoporn.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//section[@class='outer-section']").SelectSingleNodeOrThrow(".//h2").InnerText.Split("porn")[0].Trim();
        var images = soup.SelectNodesOrThrow("//div[@class='thumb grid-item']").Select(img => img.SelectSingleNodeOrThrow(".//img").GetSrc().Replace("tn_", "")).ToStringImageLinkWrapperList();
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}