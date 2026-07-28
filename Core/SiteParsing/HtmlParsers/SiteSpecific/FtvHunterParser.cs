using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;

public class FtvHunterParser : HeaderTitleListGalleryParser, IHtmlParser
{
    public static string ParserName => "ftvhunter";

    public FtvHunterParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders,
                           FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager,
        requestHeaders, IHtmlParser.GetFilenameScheme<FtvHunterParser>(filenameScheme))
    {
    }
}