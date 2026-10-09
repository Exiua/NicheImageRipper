
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using NicheImageRipper.SiteModules.Modules.Generic;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.EliteBabes;
public class EliteBabesParser : ListGalleryStaticParser, IHtmlParser
{
    public static string ParserName => "elitebabes";
    public static string[] SupportedUrls => ["https://www.elitebabes.com/"];

    public EliteBabesParser(WebDriver driver,  Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver,  requestHeaders, IHtmlParser.GetFilenameScheme<EliteBabesParser>(filenameScheme))
    {
    }
}