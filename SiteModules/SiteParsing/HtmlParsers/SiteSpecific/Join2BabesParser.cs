using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.SiteParsing.HtmlParsers.SiteSpecific;
// join2babes.com
public class Join2BabesParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "join2babes";
    public static string[] SupportedUrls => ["https://www.join2babes.com/"];
    protected override string DirNameXpath => "//div[@class='gallery_title_div']//h1";
    protected override string ImageContainerXpath => "//div[@class='gthumbs']";

    public Join2BabesParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<Join2BabesParser>(filenameScheme))
    {
    }
}