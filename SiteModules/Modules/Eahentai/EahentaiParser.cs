

using Sdk.DataStructures;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.Eahentai;
public class EahentaiParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "eahentai";
    public static string[] SupportedUrls => ["https://eahentai.com/"];

    public EahentaiParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<EahentaiParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for eahentai.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(delay: 1000, lazyLoadArgs: new LazyLoadArgs(), cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//h2").InnerText;
        var images = soup.SelectSingleNodeOrThrow("//div[@class='gallery']").SelectNodesOrThrow(".//a").Select(img => img.SelectSingleNodeOrThrow(".//img").GetSrc().Remove("/thumbnail").Replace("t.", ".")).Select(dummy => (StringFileLinkWrapper)dummy).ToList();
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}