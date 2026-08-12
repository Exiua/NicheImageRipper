using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;
public class CoomerParser : DotPartyParser, IHtmlParser
{
    public static string ParserName => "coomer";
    public static string[] SupportedUrls => ["https://coomer.party/", "https://coomer.su/", "https://coomer.st/"];

    public CoomerParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<CoomerParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for coomer.su and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override Task<RipInfo> ParseCore(CancellationToken cancellationToken = default)
    {
        return DotPartyParse("https://coomer.st", cancellationToken);
    }
}