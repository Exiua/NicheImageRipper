
using NicheImageRipper.SiteModules.Modules.Generic;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.HegreHunter;
public class HegreHunterParser : HeaderTitleListGalleryParser, IHtmlParser
{
    public static string ParserName => "hegrehunter";
    public static string[] SupportedUrls => ["https://www.hegrehunter.com/"];

    public HegreHunterParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<HegreHunterParser>(filenameScheme))
    {
    }
}