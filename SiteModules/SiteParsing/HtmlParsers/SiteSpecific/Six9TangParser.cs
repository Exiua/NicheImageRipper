using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.SiteParsing.HtmlParsers.SiteSpecific;
public class Six9TangParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "69tang";
    public static string[] SupportedUrls => ["https://www.69tang.org/"];

    public Six9TangParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<Six9TangParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for 69tang.org and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//div[@class='headline']/h1").InnerText;
        var video = soup.SelectSingleNodeOrThrow("//div[@class='fp-player']/video").GetSrc();
        return RipInfo.FromUrlList([video], dirName, FilenameScheme);
    }
}