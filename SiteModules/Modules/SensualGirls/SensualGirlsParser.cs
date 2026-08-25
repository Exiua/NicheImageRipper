
using NicheImageRipper.SiteModules.Modules.Generic;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.SensualGirls;
// sensualgirls.org
public class SensualGirlsParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "sensualgirls";
    public static string[] SupportedUrls => ["https://www.sensualgirls.org/"];
    protected override string DirNameXpath => "//a[@class='albumName']";
    protected override string ImageContainerXpath => "//div[@id='box_289']//div[@class='gbox']";

    public SensualGirlsParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<SensualGirlsParser>(filenameScheme))
    {
    }
}