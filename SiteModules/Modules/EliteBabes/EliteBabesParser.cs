using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using NicheImageRipper.Core.SiteParsing.HtmlParsers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.EliteBabes;
public class EliteBabesParser : ListGalleryStaticParser, IHtmlParser
{
    public static string ParserName => "elitebabes";
    public static string[] SupportedUrls => ["https://www.elitebabes.com/"];

    public EliteBabesParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<EliteBabesParser>(filenameScheme))
    {
    }
}