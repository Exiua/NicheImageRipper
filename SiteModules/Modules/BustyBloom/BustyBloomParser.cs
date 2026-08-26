
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using NicheImageRipper.SiteModules.Modules.Generic;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.BustyBloom;
public class BustyBloomParser : ClickToEnlargeGalleryParser, IHtmlParser
{
    public static string ParserName => "bustybloom";
    public static string[] SupportedUrls => ["https://www.bustybloom.com/"];

    public BustyBloomParser(WebDriver driver,  Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver,  requestHeaders, IHtmlParser.GetFilenameScheme<BustyBloomParser>(filenameScheme))
    {
    }
}