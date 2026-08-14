using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.SiteParsing.HtmlParsers.SiteSpecific;
// nudity911.com
public class Nudity911Parser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "nudity911";
    public static string[] SupportedUrls => ["https://www.nudity911.com/"];
    protected override string DirNameXpath => "//h1";
    protected override string ImageContainerXpath => "//tr[@valign='top']//td[@align='center']//table[@width='650']//td[@width='33%']";

    public Nudity911Parser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<Nudity911Parser>(filenameScheme))
    {
    }
}