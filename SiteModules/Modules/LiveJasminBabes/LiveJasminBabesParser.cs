
using NicheImageRipper.SiteModules.Modules.Generic;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.LiveJasminBabes;
// livejasminbabes.net
public class LiveJasminBabesParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "livejasminbabes";
    public static string[] SupportedUrls => ["https://www.livejasminbabes.net/"];
    protected override string DirNameXpath => "//div[@id='gallery_header']//h1";
    protected override string ImageContainerXpath => "//div[@class='gallery_thumb']";

    public LiveJasminBabesParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<LiveJasminBabesParser>(filenameScheme))
    {
    }
}