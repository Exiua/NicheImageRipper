
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using NicheImageRipper.SiteModules.Modules.Generic;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.DirtyYoungBitches;
public class DirtyYoungBitchesParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "dirtyyoungbitches";
    public static string[] SupportedUrls => ["https://www.dirtyyoungbitches.com/"];
    protected override string DirNameXpath => "//div[@class='title-holder']//h1";
    protected override string ImageContainerXpath => "//div[@class='container cont-light']//div[@class='images']//a[@class='thumb']";

    public DirtyYoungBitchesParser(WebDriver driver,  Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver,  requestHeaders, IHtmlParser.GetFilenameScheme<DirtyYoungBitchesParser>(filenameScheme))
    {
    }
}