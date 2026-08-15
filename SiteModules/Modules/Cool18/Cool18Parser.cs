using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.Cool18;
public class Cool18Parser : HtmlParser, IHtmlParser
{
    public static string ParserName => "cool18";
    public static string[] SupportedUrls => ["https://www.cool18.com/"];

    public Cool18Parser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<Cool18Parser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for site and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(cancellationToken: cancellationToken);
        var showContent = soup.SelectSingleNodeOrThrow("//td[@class='show_content']");
        var dirName = showContent.SelectSingleNodeOrThrow(".//b").InnerText;
        var images = showContent.SelectSingleNodeOrThrow(".//pre").SelectNodesOrThrow(".//img").Select(img => img.GetSrc()).Select(dummy => (StringFileLinkWrapper)dummy).ToList();
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}