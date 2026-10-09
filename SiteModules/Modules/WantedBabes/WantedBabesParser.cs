
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using NicheImageRipper.SiteModules.Modules.Generic;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.WantedBabes;
// wantedbabes.com
public class WantedBabesParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "wantedbabes";
    public static string[] SupportedUrls => ["https://www.wantedbabes.com/"];
    protected override string DirNameXpath => "//div[@id='main-content']//h1";
    protected override string ImageContainerXpath => "//div[@class='gallery']";

    public WantedBabesParser(WebDriver driver,  Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver,  requestHeaders, IHtmlParser.GetFilenameScheme<WantedBabesParser>(filenameScheme))
    {
    }
}