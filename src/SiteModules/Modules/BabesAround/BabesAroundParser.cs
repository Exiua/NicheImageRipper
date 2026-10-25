
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using NicheImageRipper.SiteModules.Modules.Generic;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.BabesAround;
public class BabesAroundParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "babesaround";
    public static string[] SupportedUrls => ["https://www.babesaround.com/"];
    protected override string DirNameXpath => "//section[@class='outer-section']//h2";
    protected override string ImageContainerXpath => "//div[@class='lightgallery thumbs quadruple fivefold']";

    public BabesAroundParser(WebDriver driver,  Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver,  requestHeaders, IHtmlParser.GetFilenameScheme<BabesAroundParser>(filenameScheme))
    {
    }
}