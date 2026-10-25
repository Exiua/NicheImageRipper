
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using NicheImageRipper.SiteModules.Modules.Generic;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.SexyNakeds;
// sexynakeds.com
public class SexyNakedsParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "sexynakeds";
    public static string[] SupportedUrls => ["https://www.sexynakeds.com/"];
    protected override string DirNameXpath => "(//div[@class='box']//h1)[2]";
    protected override string ImageContainerXpath => "//div[@class='post_tn']";

    public SexyNakedsParser(WebDriver driver,  Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver,  requestHeaders, IHtmlParser.GetFilenameScheme<SexyNakedsParser>(filenameScheme))
    {
    }
}