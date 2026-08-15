using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using NicheImageRipper.Core.SiteParsing.HtmlParsers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.MetArtHunter;
public class MetArtHunterParser : HeaderTitleListGalleryParser, IHtmlParser
{
    public static string ParserName => "metarthunter";
    public static string[] SupportedUrls => ["https://www.metarthunter.com/"];

    public MetArtHunterParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<MetArtHunterParser>(filenameScheme))
    {
    }
}