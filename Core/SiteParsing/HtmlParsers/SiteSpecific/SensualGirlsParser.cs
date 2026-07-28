using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;

// sensualgirls.org
public class SensualGirlsParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "sensualgirls";

    protected override string DirNameXpath => "//a[@class='albumName']";
    protected override string ImageContainerXpath => "//div[@id='box_289']//div[@class='gbox']";

    public SensualGirlsParser(WebDriver driver, ApiClientManager clientManager,
                              Dictionary<string, string> requestHeaders,
                              FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager,
        requestHeaders, IHtmlParser.GetFilenameScheme<SensualGirlsParser>(filenameScheme))
    {
    }
}