
using NicheImageRipper.SiteModules.Modules.Generic;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.RabbitsFun;
// rabbitsfun.com
public class RabbitsFunParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "rabbitsfun";
    public static string[] SupportedUrls => ["https://www.rabbitsfun.com/"];
    protected override string DirNameXpath => "//h3[@class='watch-mobTitle']";
    protected override string ImageContainerXpath => "//div[@class='gallery-watch']//li";

    public RabbitsFunParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<RabbitsFunParser>(filenameScheme))
    {
    }
}