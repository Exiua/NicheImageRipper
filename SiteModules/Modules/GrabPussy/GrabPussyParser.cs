
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using NicheImageRipper.SiteModules.Modules.Generic;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.GrabPussy;
// grabpussy.com
public class GrabPussyParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "grabpussy";
    public static string[] SupportedUrls => ["https://www.grabpussy.com/"];
    protected override string DirNameXpath => "(//div[@class='c-title'])[2]//h1";
    protected override string ImageContainerXpath => "//div[@class='gal own-gallery-images']/a";

    public GrabPussyParser(WebDriver driver,  Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver,  requestHeaders, IHtmlParser.GetFilenameScheme<GrabPussyParser>(filenameScheme))
    {
    }
}