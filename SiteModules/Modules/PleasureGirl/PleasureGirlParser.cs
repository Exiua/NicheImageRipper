using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using NicheImageRipper.Core.SiteParsing.HtmlParsers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.PleasureGirl;
// pleasuregirl.net
public class PleasureGirlParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "pleasuregirl";
    public static string[] SupportedUrls => ["https://www.pleasuregirl.net/"];
    protected override string DirNameXpath => "//h2[@class='title']";
    protected override string ImageContainerXpath => "//div[@class='lightgallery-wrap']//div[@class='grid-item thumb']";

    public PleasureGirlParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<PleasureGirlParser>(filenameScheme))
    {
    }
}