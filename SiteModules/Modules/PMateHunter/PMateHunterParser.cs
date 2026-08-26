
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using NicheImageRipper.SiteModules.Modules.Generic;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.PMateHunter;
public class PMateHunterParser : HeaderTitleListGalleryParser, IHtmlParser
{
    public static string ParserName => "pmatehunter";
    public static string[] SupportedUrls => ["https://pmatehunter.com/"];

    public PMateHunterParser(WebDriver driver,  Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver,  requestHeaders, IHtmlParser.GetFilenameScheme<PMateHunterParser>(filenameScheme))
    {
    }
}