
using NicheImageRipper.SiteModules.Modules.Generic;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.XArtHunter;
public class XArtHunterParser : HeaderTitleListGalleryParser, IHtmlParser
{
    public static string ParserName => "xarthunter";
    public static string[] SupportedUrls => ["https://www.xarthunter.com/"];

    public XArtHunterParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<XArtHunterParser>(filenameScheme))
    {
    }
}