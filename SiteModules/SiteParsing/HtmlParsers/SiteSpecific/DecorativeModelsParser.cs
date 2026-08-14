using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.SiteParsing.HtmlParsers.SiteSpecific;
public class DecorativeModelsParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "decorativemodels";
    public static string[] SupportedUrls => ["https://www.decorativemodels.com/"];
    protected override string DirNameXpath => "//h1[@class='center']";
    protected override string ImageContainerXpath => "//div[@class='list gallery']";

    public DecorativeModelsParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<DecorativeModelsParser>(filenameScheme))
    {
    }
}