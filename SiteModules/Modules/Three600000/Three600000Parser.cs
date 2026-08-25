

using Sdk.DataStructures;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.Three600000;
public class Three600000Parser : HtmlParser, IHtmlParser
{
    public static string ParserName => "3600000";
    public static string[] SupportedUrls => ["https://3600000.xyz/"];

    public Three600000Parser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<Three600000Parser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for 3600000.xyz and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='entry-title']").InnerText;
        var images = soup.SelectSingleNodeOrThrow("//div[@class='entry-content']/p").SelectNodesOrThrow("./a").Select(a => a.GetHref()).ToStringFileLinkWrapperList();
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}