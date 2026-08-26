
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using NicheImageRipper.SiteModules.Modules.Generic;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.RabbitsFun;
// rabbitsfun.com
public class RabbitsFunParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "rabbitsfun";
    public static string[] SupportedUrls => ["https://www.rabbitsfun.com/"];
    protected override string DirNameXpath => "//h3[@class='watch-mobTitle']";
    protected override string ImageContainerXpath => "//div[@class='gallery-watch']//li";

    public RabbitsFunParser(WebDriver driver,  Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver,  requestHeaders, IHtmlParser.GetFilenameScheme<RabbitsFunParser>(filenameScheme))
    {
    }
}