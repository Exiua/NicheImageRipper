using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;

// girlsofdesire.org
public class GirlsOfDesireParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "girlsofdesire";

    protected override string DirNameXpath => "//a[@class='albumName']";
    protected override string ImageContainerXpath => "//div[@id='gal_10']//td[@class='vtop']";

    public GirlsOfDesireParser(WebDriver driver, ApiClientManager clientManager,
                               Dictionary<string, string> requestHeaders,
                               FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager,
        requestHeaders, IHtmlParser.GetFilenameScheme<GirlsOfDesireParser>(filenameScheme))
    {
    }
}