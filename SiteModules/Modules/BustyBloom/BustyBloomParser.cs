
using NicheImageRipper.SiteModules.Modules.Generic;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.BustyBloom;
public class BustyBloomParser : ClickToEnlargeGalleryParser, IHtmlParser
{
    public static string ParserName => "bustybloom";
    public static string[] SupportedUrls => ["https://www.bustybloom.com/"];

    public BustyBloomParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<BustyBloomParser>(filenameScheme))
    {
    }
}