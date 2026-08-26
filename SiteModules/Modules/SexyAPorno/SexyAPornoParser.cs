
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using NicheImageRipper.SiteModules.Modules.Generic;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.SexyAPorno;
public class SexyAPornoParser : ClickToEnlargeGalleryParser, IHtmlParser
{
    public static string ParserName => "sexyaporno";
    public static string[] SupportedUrls => ["https://www.sexyaporno.com/"];

    public SexyAPornoParser(WebDriver driver,  Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver,  requestHeaders, IHtmlParser.GetFilenameScheme<SexyAPornoParser>(filenameScheme))
    {
    }
}