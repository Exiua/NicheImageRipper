using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;

// rabbitsfun.com
public class RabbitsFunParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "rabbitsfun";

    protected override string DirNameXpath => "//h3[@class='watch-mobTitle']";
    protected override string ImageContainerXpath => "//div[@class='gallery-watch']//li";

    public RabbitsFunParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders,
                            FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager,
        requestHeaders, IHtmlParser.GetFilenameScheme<RabbitsFunParser>(filenameScheme))
    {
    }
}