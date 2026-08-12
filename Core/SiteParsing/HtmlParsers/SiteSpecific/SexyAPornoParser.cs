using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;
public class SexyAPornoParser : ClickToEnlargeGalleryParser, IHtmlParser
{
    public static string ParserName => "sexyaporno";
    public static string[] SupportedUrls => ["https://www.sexyaporno.com/"];

    public SexyAPornoParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<SexyAPornoParser>(filenameScheme))
    {
    }
}