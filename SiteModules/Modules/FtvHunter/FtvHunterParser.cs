
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using NicheImageRipper.SiteModules.Modules.Generic;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.FtvHunter;
public class FtvHunterParser : HeaderTitleListGalleryParser, IHtmlParser
{
    public static string ParserName => "ftvhunter";
    public static string[] SupportedUrls => ["https://www.ftvhunter.com/"];

    public FtvHunterParser(WebDriver driver,  Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver,  requestHeaders, IHtmlParser.GetFilenameScheme<FtvHunterParser>(filenameScheme))
    {
    }
}