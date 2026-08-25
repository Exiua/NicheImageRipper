
using NicheImageRipper.SiteModules.Modules.Generic;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.SexyAPorno;
public class SexyAPornoParser : ClickToEnlargeGalleryParser, IHtmlParser
{
    public static string ParserName => "sexyaporno";
    public static string[] SupportedUrls => ["https://www.sexyaporno.com/"];

    public SexyAPornoParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<SexyAPornoParser>(filenameScheme))
    {
    }
}