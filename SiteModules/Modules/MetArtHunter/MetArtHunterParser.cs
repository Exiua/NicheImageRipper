
using NicheImageRipper.SiteModules.Modules.Generic;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.MetArtHunter;
public class MetArtHunterParser : HeaderTitleListGalleryParser, IHtmlParser
{
    public static string ParserName => "metarthunter";
    public static string[] SupportedUrls => ["https://www.metarthunter.com/"];

    public MetArtHunterParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<MetArtHunterParser>(filenameScheme))
    {
    }
}