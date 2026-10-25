
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using NicheImageRipper.SiteModules.Modules.Generic;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.XArtHunter;
public class XArtHunterParser : HeaderTitleListGalleryParser, IHtmlParser
{
    public static string ParserName => "xarthunter";
    public static string[] SupportedUrls => ["https://www.xarthunter.com/"];

    public XArtHunterParser(WebDriver driver,  Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver,  requestHeaders, IHtmlParser.GetFilenameScheme<XArtHunterParser>(filenameScheme))
    {
    }
}