using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;

public class ErosBerryParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "erosberry";

    protected override string DirNameXpath => "//h1[@class='title']";
    protected override string ImageContainerXpath => "//div[@class='block-post three-post flex']";

    public ErosBerryParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders,
                           FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager,
        requestHeaders, IHtmlParser.GetFilenameScheme<ErosBerryParser>(filenameScheme))
    {
    }
}