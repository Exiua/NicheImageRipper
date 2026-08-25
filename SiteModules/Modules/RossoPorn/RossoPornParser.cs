
using NicheImageRipper.SiteModules.Modules.Generic;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.RossoPorn;
// rossoporn.com
public class RossoPornParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "rossoporn";
    public static string[] SupportedUrls => ["https://www.rossoporn.com/"];
    protected override string DirNameXpath => "//div[@class='content_right']//h1";
    protected override string ImageContainerXpath => "//div[@class='wrapper_g']";

    public RossoPornParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<RossoPornParser>(filenameScheme))
    {
    }
}