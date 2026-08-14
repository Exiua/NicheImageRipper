using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.SiteParsing.HtmlParsers.SiteSpecific;
public class SankakuComplexParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "sankakucomplex";
    public static string[] SupportedUrls => ["https://www.sankakucomplex.com/"];

    public SankakuComplexParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<SankakuComplexParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for sankakucomplex.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns></returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//header[@class='entry-header']/h1[@class='entry-title']/a").InnerText;
        var imagesBase = soup.SelectNodesOrThrow("//a[@class='swipebox']").GetHrefs()[1..];
        var images = imagesBase.Select(image => !image.Contains("http") ? $"https:{image}" : image).Select(dummy => (StringFileLinkWrapper)dummy).ToList();
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}