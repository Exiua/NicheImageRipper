

using Sdk.DataStructures;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.Xiuren;
public class XiurenParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "xiuren";
    public static string[] SupportedUrls => ["https://xiuren.biz/"];

    public XiurenParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = Sdk.Enums.FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<XiurenParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for xiuren.biz and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='jeg_post_title']").InnerText;
        var images = soup.SelectSingleNodeOrThrow("//div[@class='content-inner ']").SelectNodesOrThrow(".//a").Select(img => img.GetHref()).ToStringFileLinkWrapperList();
        return RipInfo.FromUrlList(images, dirName, Sdk.Enums.FilenameScheme);
    }
}