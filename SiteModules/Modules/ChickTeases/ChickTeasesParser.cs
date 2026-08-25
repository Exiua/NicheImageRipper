
using NicheImageRipper.SiteModules.Modules.Generic;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.ChickTeases;
public class ChickTeasesParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "chickteases";
    public static string[] SupportedUrls => ["https://www.chickteases.com/"];
    protected override string DirNameXpath => "//h1[@id='galleryModelName']";
    protected override string ImageContainerXpath => "//div[@class='minithumbs']";

    public ChickTeasesParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<ChickTeasesParser>(filenameScheme))
    {
    }
}