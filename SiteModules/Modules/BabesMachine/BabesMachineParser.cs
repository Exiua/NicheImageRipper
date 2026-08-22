using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.BabesMachine;
public class BabesMachineParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "babesmachine";
    public static string[] SupportedUrls => ["https://www.babesmachine.com/"];

    public BabesMachineParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<BabesMachineParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for babesmachine.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(cancellationToken: cancellationToken);
        var gallery = soup.SelectSingleNodeOrThrow("//div[@id='gallery']");
        var dirName = gallery.SelectSingleNodeOrThrow(".//h2").SelectSingleNodeOrThrow(".//a").InnerText;
        var images = gallery.SelectSingleNodeOrThrow(".//table").SelectNodesOrThrow(".//tr").Select(img => img.SelectSingleNodeOrThrow(".//img").GetSrc().Remove("tn_")).Select(img => Protocol + img).ToStringFileLinkWrapperList();
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}