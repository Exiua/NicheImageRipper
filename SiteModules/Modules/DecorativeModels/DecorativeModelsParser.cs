
using NicheImageRipper.SiteModules.Modules.Generic;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.DecorativeModels;
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