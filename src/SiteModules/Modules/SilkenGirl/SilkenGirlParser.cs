
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using NicheImageRipper.SiteModules.Modules.Generic;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.SilkenGirl;
// silkengirl.com and silkengirl.net
public class SilkenGirlParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "silkengirl";
    public static string[] SupportedUrls => ["https://www.silkengirl.com/"];
    protected override string DirNameXpath => "//h1[@class='title']|//div[@class='content_main']//h2";
    protected override string ImageContainerXpath => "//div[@class='thumb_box']";

    public SilkenGirlParser(WebDriver driver,  Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver,  requestHeaders, IHtmlParser.GetFilenameScheme<SilkenGirlParser>(filenameScheme))
    {
    }
}