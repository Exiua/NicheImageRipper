using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;
// novoglam.com
public class NovoGlamParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "novoglam";
    public static string[] SupportedUrls => ["https://www.novoglam.com/"];
    protected override string DirNameXpath => "//div[@id='heading']//h1";
    protected override string ImageContainerXpath => "//ul[@id='myGalleryThumbs']";

    public NovoGlamParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<NovoGlamParser>(filenameScheme))
    {
    }
}