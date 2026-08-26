
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using NicheImageRipper.SiteModules.Modules.Generic;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.FemJoyHunter;
public class FemJoyHunterParser : HeaderTitleListGalleryParser, IHtmlParser
{
    public static string ParserName => "femjoyhunter";
    public static string[] SupportedUrls => ["https://www.femjoyhunter.com/"];

    public FemJoyHunterParser(WebDriver driver,  Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver,  requestHeaders, IHtmlParser.GetFilenameScheme<FemJoyHunterParser>(filenameScheme))
    {
    }
}