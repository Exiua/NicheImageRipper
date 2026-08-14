using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.SiteParsing.HtmlParsers.SiteSpecific;
// nightdreambabe.com
public class NightDreamBabeParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "nightdreambabe";
    public static string[] SupportedUrls => ["https://www.nightdreambabe.com/"];
    protected override string DirNameXpath => "//section[@class='outer-section']//h2[@class='section-title title']";
    protected override string ImageContainerXpath => "//div[@class='lightgallery thumbs quadruple fivefold']//a[@class='gallery-card']";

    public NightDreamBabeParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<NightDreamBabeParser>(filenameScheme))
    {
    }
}