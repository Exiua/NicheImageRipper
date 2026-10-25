
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using NicheImageRipper.SiteModules.Modules.Generic;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.DecorativeModels;
public class DecorativeModelsParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "decorativemodels";
    public static string[] SupportedUrls => ["https://www.decorativemodels.com/"];
    protected override string DirNameXpath => "//h1[@class='center']";
    protected override string ImageContainerXpath => "//div[@class='list gallery']";

    public DecorativeModelsParser(WebDriver driver,  Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver,  requestHeaders, IHtmlParser.GetFilenameScheme<DecorativeModelsParser>(filenameScheme))
    {
    }
}