
using NicheImageRipper.SiteModules.Modules.Generic;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.NovoHot;
// novohot.com
public class NovoHotParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "novohot";
    public static string[] SupportedUrls => ["https://www.novohot.com/"];
    protected override string DirNameXpath => "//div[@id='viewIMG']//h1";
    protected override string ImageContainerXpath => "//div[@class='runout']/a";

    public NovoHotParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<NovoHotParser>(filenameScheme))
    {
    }
}