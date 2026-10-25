
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using NicheImageRipper.SiteModules.Modules.Generic;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.SensualGirls;
// sensualgirls.org
public class SensualGirlsParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "sensualgirls";
    public static string[] SupportedUrls => ["https://www.sensualgirls.org/"];
    protected override string DirNameXpath => "//a[@class='albumName']";
    protected override string ImageContainerXpath => "//div[@id='box_289']//div[@class='gbox']";

    public SensualGirlsParser(WebDriver driver,  Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver,  requestHeaders, IHtmlParser.GetFilenameScheme<SensualGirlsParser>(filenameScheme))
    {
    }
}