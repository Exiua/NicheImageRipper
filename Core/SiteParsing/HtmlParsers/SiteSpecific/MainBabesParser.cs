using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;
// mainbabes.com
public class MainBabesParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "mainbabes";
    public static string[] SupportedUrls => ["https://www.mainbabes.com/"];
    protected override string DirNameXpath => "//div[@class='heading']//h2[@class='title']";
    protected override string ImageContainerXpath => "//div[@class='thumbs_box']//div[@class='thumb_box']";

    public MainBabesParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<MainBabesParser>(filenameScheme))
    {
    }
}