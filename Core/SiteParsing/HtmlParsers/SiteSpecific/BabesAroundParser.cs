using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;
public class BabesAroundParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "babesaround";
    public static string[] SupportedUrls => ["https://www.babesaround.com/"];
    protected override string DirNameXpath => "//section[@class='outer-section']//h2";
    protected override string ImageContainerXpath => "//div[@class='lightgallery thumbs quadruple fivefold']";

    public BabesAroundParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<BabesAroundParser>(filenameScheme))
    {
    }
}