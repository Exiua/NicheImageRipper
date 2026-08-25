
using NicheImageRipper.SiteModules.Modules.Generic;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.PMateHunter;
public class PMateHunterParser : HeaderTitleListGalleryParser, IHtmlParser
{
    public static string ParserName => "pmatehunter";
    public static string[] SupportedUrls => ["https://pmatehunter.com/"];

    public PMateHunterParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<PMateHunterParser>(filenameScheme))
    {
    }
}