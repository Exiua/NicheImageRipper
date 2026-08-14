using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.SiteParsing.HtmlParsers.SiteSpecific;
public class ChickTeasesParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "chickteases";
    public static string[] SupportedUrls => ["https://www.chickteases.com/"];
    protected override string DirNameXpath => "//h1[@id='galleryModelName']";
    protected override string ImageContainerXpath => "//div[@class='minithumbs']";

    public ChickTeasesParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<ChickTeasesParser>(filenameScheme))
    {
    }
}