using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.SiteParsing.HtmlParsers.SiteSpecific;
public class BabesBangParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "babesbang";
    public static string[] SupportedUrls => ["https://www.babesbang.com/"];
    protected override string DirNameXpath => "//div[@class='main-title']";
    protected override string ImageContainerXpath => "//div[@class='gal-block']";

    public BabesBangParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<BabesBangParser>(filenameScheme))
    {
    }
}