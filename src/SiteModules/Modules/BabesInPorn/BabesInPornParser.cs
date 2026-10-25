
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using NicheImageRipper.SiteModules.Modules.Generic;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.BabesInPorn;
public class BabesInPornParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "babesinporn";
    public static string[] SupportedUrls => ["https://www.babesinporn.com/"];
    protected override string DirNameXpath => "//h1[@class='blockheader pink center lowercase']";
    protected override string ImageContainerXpath => "//div[@class='list gallery']";

    public BabesInPornParser(WebDriver driver,  Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver,  requestHeaders, IHtmlParser.GetFilenameScheme<BabesInPornParser>(filenameScheme))
    {
    }
}