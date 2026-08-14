using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.SiteParsing.HtmlParsers.SiteSpecific;
public class JoyMiiHubParser : HeaderTitleListGalleryParser, IHtmlParser
{
    public static string ParserName => "joymiihub";
    public static string[] SupportedUrls => ["https://www.joymiihub.com/"];

    public JoyMiiHubParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<JoyMiiHubParser>(filenameScheme))
    {
    }
}