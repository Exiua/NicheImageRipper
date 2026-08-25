
using NicheImageRipper.SiteModules.Modules.Generic;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.BabesBang;
public class BabesBangParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "babesbang";
    public static string[] SupportedUrls => ["https://www.babesbang.com/"];
    protected override string DirNameXpath => "//div[@class='main-title']";
    protected override string ImageContainerXpath => "//div[@class='gal-block']";

    public BabesBangParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<BabesBangParser>(filenameScheme))
    {
    }
}