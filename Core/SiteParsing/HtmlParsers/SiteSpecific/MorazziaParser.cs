using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;

// morazzia.com
public class MorazziaParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "morazzia";

    protected override string DirNameXpath => "//h1[@class='title']";
    protected override string ImageContainerXpath => "//div[@class='block-post album-item']//a";

    public MorazziaParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders,
                          FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager,
        requestHeaders, IHtmlParser.GetFilenameScheme<MorazziaParser>(filenameScheme))
    {
    }
}