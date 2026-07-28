using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;

// nightdreambabe.com
public class NightDreamBabeParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "nightdreambabe";

    protected override string DirNameXpath => "//section[@class='outer-section']//h2[@class='section-title title']";
    protected override string ImageContainerXpath => "//div[@class='lightgallery thumbs quadruple fivefold']//a[@class='gallery-card']";

    public NightDreamBabeParser(WebDriver driver, ApiClientManager clientManager,
                                Dictionary<string, string> requestHeaders,
                                FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager,
        requestHeaders, IHtmlParser.GetFilenameScheme<NightDreamBabeParser>(filenameScheme))
    {
    }
}