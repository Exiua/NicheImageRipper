
using NicheImageRipper.SiteModules.Modules.Generic;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.JoyMiiHub;
public class JoyMiiHubParser : HeaderTitleListGalleryParser, IHtmlParser
{
    public static string ParserName => "joymiihub";
    public static string[] SupportedUrls => ["https://www.joymiihub.com/"];

    public JoyMiiHubParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<JoyMiiHubParser>(filenameScheme))
    {
    }
}