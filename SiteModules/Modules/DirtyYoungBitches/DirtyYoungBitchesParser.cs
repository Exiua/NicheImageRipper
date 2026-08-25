
using NicheImageRipper.SiteModules.Modules.Generic;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.DirtyYoungBitches;
public class DirtyYoungBitchesParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "dirtyyoungbitches";
    public static string[] SupportedUrls => ["https://www.dirtyyoungbitches.com/"];
    protected override string DirNameXpath => "//div[@class='title-holder']//h1";
    protected override string ImageContainerXpath => "//div[@class='container cont-light']//div[@class='images']//a[@class='thumb']";

    public DirtyYoungBitchesParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<DirtyYoungBitchesParser>(filenameScheme))
    {
    }
}