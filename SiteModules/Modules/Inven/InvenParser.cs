

using Sdk.DataStructures;
using Sdk.Enums;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.Inven;
public class InvenParser : HtmlParser, IHtmlParser, ISubdomainSignificantHtmlParser, IRefererOverrideHtmlParser
{
    public static string ParserName => "inven";
    public static string[] SupportedUrls => ["https://www.inven.co.kr/"];
    public static int SignificantDomainLabels => 3;
    public static string RefererOverride => "";

    public InvenParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<InvenParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for inven.co.kr and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//div[@class='subject ']//span[@class='middle']").InnerText;
        var images = soup.SelectSingleNodeOrThrow("//div[@id='powerbbsContent']").SelectNodesOrThrow(".//img").Select(img => img.GetSrc().Split("?")[0]).ToStringFileLinkWrapperList();
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}