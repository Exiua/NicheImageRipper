
using NicheImageRipper.SiteModules.Modules.Generic;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.MainBabes;
// mainbabes.com
public class MainBabesParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "mainbabes";
    public static string[] SupportedUrls => ["https://www.mainbabes.com/"];
    protected override string DirNameXpath => "//div[@class='heading']//h2[@class='title']";
    protected override string ImageContainerXpath => "//div[@class='thumbs_box']//div[@class='thumb_box']";

    public MainBabesParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<MainBabesParser>(filenameScheme))
    {
    }
}