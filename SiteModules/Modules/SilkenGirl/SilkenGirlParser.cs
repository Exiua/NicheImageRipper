
using NicheImageRipper.SiteModules.Modules.Generic;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.SilkenGirl;
// silkengirl.com and silkengirl.net
public class SilkenGirlParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "silkengirl";
    public static string[] SupportedUrls => ["https://www.silkengirl.com/"];
    protected override string DirNameXpath => "//h1[@class='title']|//div[@class='content_main']//h2";
    protected override string ImageContainerXpath => "//div[@class='thumb_box']";

    public SilkenGirlParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<SilkenGirlParser>(filenameScheme))
    {
    }
}