
using NicheImageRipper.SiteModules.Modules.Generic;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.GirlsOfDesire;
// girlsofdesire.org
public class GirlsOfDesireParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "girlsofdesire";
    public static string[] SupportedUrls => ["https://www.girlsofdesire.org/"];
    protected override string DirNameXpath => "//a[@class='albumName']";
    protected override string ImageContainerXpath => "//div[@id='gal_10']//td[@class='vtop']";

    public GirlsOfDesireParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<GirlsOfDesireParser>(filenameScheme))
    {
    }
}