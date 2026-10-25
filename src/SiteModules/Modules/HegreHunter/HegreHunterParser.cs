
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using NicheImageRipper.SiteModules.Modules.Generic;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.HegreHunter;
public class HegreHunterParser : HeaderTitleListGalleryParser, IHtmlParser
{
    public static string ParserName => "hegrehunter";
    public static string[] SupportedUrls => ["https://www.hegrehunter.com/"];

    public HegreHunterParser(WebDriver driver,  Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver,  requestHeaders, IHtmlParser.GetFilenameScheme<HegreHunterParser>(filenameScheme))
    {
    }
}