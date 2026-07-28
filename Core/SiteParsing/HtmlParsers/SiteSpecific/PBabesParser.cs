using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;

// pbabes.com
public class PBabesParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "pbabes";

    protected override string DirNameXpath => "(//div[@class='box_654'])[2]//h1";
    protected override string ImageContainerXpath => "//div[@style='margin-left:35px;']//a[@rel='nofollow']";

    public PBabesParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders,
                        FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager,
        requestHeaders, IHtmlParser.GetFilenameScheme<PBabesParser>(filenameScheme))
    {
    }
}