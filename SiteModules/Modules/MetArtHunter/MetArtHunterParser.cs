
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using NicheImageRipper.SiteModules.Modules.Generic;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.MetArtHunter;
public class MetArtHunterParser : HeaderTitleListGalleryParser, IHtmlParser
{
    public static string ParserName => "metarthunter";
    public static string[] SupportedUrls => ["https://www.metarthunter.com/"];

    public MetArtHunterParser(WebDriver driver,  Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver,  requestHeaders, IHtmlParser.GetFilenameScheme<MetArtHunterParser>(filenameScheme))
    {
    }
}