using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using NicheImageRipper.Core.SiteParsing.HtmlParsers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.HegreHunter;
public class HegreHunterParser : HeaderTitleListGalleryParser, IHtmlParser
{
    public static string ParserName => "hegrehunter";
    public static string[] SupportedUrls => ["https://www.hegrehunter.com/"];

    public HegreHunterParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<HegreHunterParser>(filenameScheme))
    {
    }
}