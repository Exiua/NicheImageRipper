using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;
public class DecorativeModelsParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "decorativemodels";
    public static string[] SupportedUrls => ["https://www.decorativemodels.com/"];
    protected override string DirNameXpath => "//h1[@class='center']";
    protected override string ImageContainerXpath => "//div[@class='list gallery']";

    public DecorativeModelsParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<DecorativeModelsParser>(filenameScheme))
    {
    }
}