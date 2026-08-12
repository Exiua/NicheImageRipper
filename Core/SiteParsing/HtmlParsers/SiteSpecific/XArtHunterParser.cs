using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;
public class XArtHunterParser : HeaderTitleListGalleryParser, IHtmlParser
{
    public static string ParserName => "xarthunter";
    public static string[] SupportedUrls => ["https://www.xarthunter.com/"];

    public XArtHunterParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<XArtHunterParser>(filenameScheme))
    {
    }
}