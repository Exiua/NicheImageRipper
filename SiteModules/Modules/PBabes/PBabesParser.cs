
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using NicheImageRipper.SiteModules.Modules.Generic;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.PBabes;
// pbabes.com
public class PBabesParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "pbabes";
    public static string[] SupportedUrls => ["https://www.pbabes.com/"];
    protected override string DirNameXpath => "(//div[@class='box_654'])[2]//h1";
    protected override string ImageContainerXpath => "//div[@style='margin-left:35px;']//a[@rel='nofollow']";

    public PBabesParser(WebDriver driver,  Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver,  requestHeaders, IHtmlParser.GetFilenameScheme<PBabesParser>(filenameScheme))
    {
    }
}