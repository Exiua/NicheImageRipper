using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;
public class PMateHunterParser : HeaderTitleListGalleryParser, IHtmlParser
{
    public static string ParserName => "pmatehunter";
    public static string[] SupportedUrls => ["https://pmatehunter.com/"];

    public PMateHunterParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<PMateHunterParser>(filenameScheme))
    {
    }
}