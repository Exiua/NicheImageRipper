
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using NicheImageRipper.SiteModules.Modules.Generic;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.NovoHot;
// novohot.com
public class NovoHotParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "novohot";
    public static string[] SupportedUrls => ["https://www.novohot.com/"];
    protected override string DirNameXpath => "//div[@id='viewIMG']//h1";
    protected override string ImageContainerXpath => "//div[@class='runout']/a";

    public NovoHotParser(WebDriver driver,  Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver,  requestHeaders, IHtmlParser.GetFilenameScheme<NovoHotParser>(filenameScheme))
    {
    }
}