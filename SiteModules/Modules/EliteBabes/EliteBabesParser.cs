
using NicheImageRipper.SiteModules.Modules.Generic;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.EliteBabes;
public class EliteBabesParser : ListGalleryStaticParser, IHtmlParser
{
    public static string ParserName => "elitebabes";
    public static string[] SupportedUrls => ["https://www.elitebabes.com/"];

    public EliteBabesParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<EliteBabesParser>(filenameScheme))
    {
    }
}