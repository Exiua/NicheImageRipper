using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;

// rossoporn.com
public class RossoPornParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "rossoporn";

    protected override string DirNameXpath => "//div[@class='content_right']//h1";
    protected override string ImageContainerXpath => "//div[@class='wrapper_g']";

    public RossoPornParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders,
                           FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager,
        requestHeaders, IHtmlParser.GetFilenameScheme<RossoPornParser>(filenameScheme))
    {
    }
}