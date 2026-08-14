using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.SiteParsing.HtmlParsers.SiteSpecific;
// girlsofdesire.org
public class GirlsOfDesireParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "girlsofdesire";
    public static string[] SupportedUrls => ["https://www.girlsofdesire.org/"];
    protected override string DirNameXpath => "//a[@class='albumName']";
    protected override string ImageContainerXpath => "//div[@id='gal_10']//td[@class='vtop']";

    public GirlsOfDesireParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<GirlsOfDesireParser>(filenameScheme))
    {
    }
}