using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;
public class SexyBabesArtParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "sexybabesart";
    public static string[] SupportedUrls => ["https://www.sexybabesart.com/"];
    protected override string DirNameXpath => "//div[@class='content-title']/h1";
    protected override string ImageContainerXpath => "//div[@class='thumbs']";

    public SexyBabesArtParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<SexyBabesArtParser>(filenameScheme))
    {
    }
}