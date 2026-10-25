
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using NicheImageRipper.SiteModules.Modules.Generic;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.BabesBang;
public class BabesBangParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "babesbang";
    public static string[] SupportedUrls => ["https://www.babesbang.com/"];
    protected override string DirNameXpath => "//div[@class='main-title']";
    protected override string ImageContainerXpath => "//div[@class='gal-block']";

    public BabesBangParser(WebDriver driver,  Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver,  requestHeaders, IHtmlParser.GetFilenameScheme<BabesBangParser>(filenameScheme))
    {
    }
}