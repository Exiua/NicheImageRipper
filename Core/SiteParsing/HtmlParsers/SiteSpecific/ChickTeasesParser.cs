using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;

public class ChickTeasesParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "chickteases";

    protected override string DirNameXpath => "//h1[@id='galleryModelName']";
    protected override string ImageContainerXpath => "//div[@class='minithumbs']";

    public ChickTeasesParser(WebDriver driver, ApiClientManager clientManager,
                             Dictionary<string, string> requestHeaders,
                             FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager,
        requestHeaders, IHtmlParser.GetFilenameScheme<ChickTeasesParser>(filenameScheme))
    {
    }
}