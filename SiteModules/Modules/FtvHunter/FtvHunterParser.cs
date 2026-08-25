
using NicheImageRipper.SiteModules.Modules.Generic;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.FtvHunter;
public class FtvHunterParser : HeaderTitleListGalleryParser, IHtmlParser
{
    public static string ParserName => "ftvhunter";
    public static string[] SupportedUrls => ["https://www.ftvhunter.com/"];

    public FtvHunterParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<FtvHunterParser>(filenameScheme))
    {
    }
}