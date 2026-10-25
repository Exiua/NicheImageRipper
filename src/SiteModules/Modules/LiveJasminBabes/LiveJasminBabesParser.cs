
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using NicheImageRipper.SiteModules.Modules.Generic;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.LiveJasminBabes;
// livejasminbabes.net
public class LiveJasminBabesParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "livejasminbabes";
    public static string[] SupportedUrls => ["https://www.livejasminbabes.net/"];
    protected override string DirNameXpath => "//div[@id='gallery_header']//h1";
    protected override string ImageContainerXpath => "//div[@class='gallery_thumb']";

    public LiveJasminBabesParser(WebDriver driver,  Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver,  requestHeaders, IHtmlParser.GetFilenameScheme<LiveJasminBabesParser>(filenameScheme))
    {
    }
}