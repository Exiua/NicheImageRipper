
using NicheImageRipper.SiteModules.Modules.Generic;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.Morazzia;
// morazzia.com
public class MorazziaParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "morazzia";
    public static string[] SupportedUrls => ["https://www.morazzia.com/"];
    protected override string DirNameXpath => "//h1[@class='title']";
    protected override string ImageContainerXpath => "//div[@class='block-post album-item']//a";

    public MorazziaParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<MorazziaParser>(filenameScheme))
    {
    }
}