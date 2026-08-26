
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using NicheImageRipper.SiteModules.Modules.Generic;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.JoyMiiHub;
public class JoyMiiHubParser : HeaderTitleListGalleryParser, IHtmlParser
{
    public static string ParserName => "joymiihub";
    public static string[] SupportedUrls => ["https://www.joymiihub.com/"];

    public JoyMiiHubParser(WebDriver driver,  Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver,  requestHeaders, IHtmlParser.GetFilenameScheme<JoyMiiHubParser>(filenameScheme))
    {
    }
}