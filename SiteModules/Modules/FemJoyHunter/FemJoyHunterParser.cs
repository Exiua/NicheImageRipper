using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using NicheImageRipper.Core.SiteParsing.HtmlParsers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.FemJoyHunter;
public class FemJoyHunterParser : HeaderTitleListGalleryParser, IHtmlParser
{
    public static string ParserName => "femjoyhunter";
    public static string[] SupportedUrls => ["https://www.femjoyhunter.com/"];

    public FemJoyHunterParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<FemJoyHunterParser>(filenameScheme))
    {
    }
}