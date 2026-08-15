using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using NicheImageRipper.Core.SiteParsing.HtmlParsers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.SexyAPorno;
public class SexyAPornoParser : ClickToEnlargeGalleryParser, IHtmlParser
{
    public static string ParserName => "sexyaporno";
    public static string[] SupportedUrls => ["https://www.sexyaporno.com/"];

    public SexyAPornoParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<SexyAPornoParser>(filenameScheme))
    {
    }
}