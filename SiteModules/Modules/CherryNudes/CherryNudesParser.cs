using NicheImageRipper.Sdk.Common.ExtensionMethods;
using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.CherryNudes;

public class CherryNudesParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "cherrynudes";
    public static string[] SupportedUrls => ["https://www.cherrynudes.com/"];

    public CherryNudesParser(WebDriver driver, Dictionary<string, string> requestHeaders,
                             FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders,
        IHtmlParser.GetFilenameScheme<CherryNudesParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for cherrynudes.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//title").InnerText.Split("-")[0].Trim();
        var contentUrl = CurrentUrl.Replace("www", "cdn");
        var images = soup.SelectSingleNodeOrThrow("//div[@class='article__gallery-images']").SelectNodesOrThrow(".//a")
                         .Select(img => img.GetHref()).ToStringFileLinkWrapperList();
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}