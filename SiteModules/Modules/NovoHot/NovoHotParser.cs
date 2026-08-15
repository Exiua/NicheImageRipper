using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using NicheImageRipper.Core.SiteParsing.HtmlParsers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.NovoHot;
// novohot.com
public class NovoHotParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "novohot";
    public static string[] SupportedUrls => ["https://www.novohot.com/"];
    protected override string DirNameXpath => "//div[@id='viewIMG']//h1";
    protected override string ImageContainerXpath => "//div[@class='runout']/a";

    public NovoHotParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<NovoHotParser>(filenameScheme))
    {
    }
}