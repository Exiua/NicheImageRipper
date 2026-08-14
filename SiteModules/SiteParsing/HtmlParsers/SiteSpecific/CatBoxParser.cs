using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.SiteParsing.HtmlParsers.SiteSpecific;
public class CatBoxParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "catbox";
    public static string[] SupportedUrls => ["https://catbox.moe/"];

    public CatBoxParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<CatBoxParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for catbox.moe and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        Logger.Warning("Catbox.moe support is experimental and may not work as expected");
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//div[@class='title']/h1").InnerText;
        var images = soup.SelectSingleNodeOrThrow("//div[@class='imagecontainer']").SelectNodesOrThrow("./video").Select(vid => vid.GetSrc()).ToStringImageLinkWrapperList();
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}