

using Sdk.DataStructures;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.NakedGirls;
public class NakedGirlsParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "nakedgirls";
    public static string[] SupportedUrls => ["https://www.nakedgirls.xxx/"];

    public NakedGirlsParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<NakedGirlsParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the HTML for nakedgirls.xxx and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//div[@class='content']").SelectSingleNodeOrThrow(".//h1").InnerText;
        var images = soup.SelectSingleNodeOrThrow("//div[@class='content']").SelectNodesOrThrow(".//div[@class='thumb']").Select(img => "https://www.nakedgirls.xxx" + img.SelectSingleNodeOrThrow(".//a").GetHref()).ToStringFileLinkWrapperList();
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}