
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using NicheImageRipper.SiteModules.Modules.Generic;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.PleasureGirl;
// pleasuregirl.net
public class PleasureGirlParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "pleasuregirl";
    public static string[] SupportedUrls => ["https://www.pleasuregirl.net/"];
    protected override string DirNameXpath => "//h2[@class='title']";
    protected override string ImageContainerXpath => "//div[@class='lightgallery-wrap']//div[@class='grid-item thumb']";

    public PleasureGirlParser(WebDriver driver,  Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver,  requestHeaders, IHtmlParser.GetFilenameScheme<PleasureGirlParser>(filenameScheme))
    {
    }
}