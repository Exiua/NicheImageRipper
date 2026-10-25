
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using NicheImageRipper.SiteModules.Modules.Generic;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.NovoGlam;
// novoglam.com
public class NovoGlamParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "novoglam";
    public static string[] SupportedUrls => ["https://www.novoglam.com/"];
    protected override string DirNameXpath => "//div[@id='heading']//h1";
    protected override string ImageContainerXpath => "//ul[@id='myGalleryThumbs']";

    public NovoGlamParser(WebDriver driver,  Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver,  requestHeaders, IHtmlParser.GetFilenameScheme<NovoGlamParser>(filenameScheme))
    {
    }
}