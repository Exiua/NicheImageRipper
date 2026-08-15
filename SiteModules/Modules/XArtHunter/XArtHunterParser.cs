using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using NicheImageRipper.Core.SiteParsing.HtmlParsers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.XArtHunter;
public class XArtHunterParser : HeaderTitleListGalleryParser, IHtmlParser
{
    public static string ParserName => "xarthunter";
    public static string[] SupportedUrls => ["https://www.xarthunter.com/"];

    public XArtHunterParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<XArtHunterParser>(filenameScheme))
    {
    }
}