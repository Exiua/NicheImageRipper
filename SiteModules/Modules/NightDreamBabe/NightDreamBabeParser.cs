
using NicheImageRipper.SiteModules.Modules.Generic;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.NightDreamBabe;
// nightdreambabe.com
public class NightDreamBabeParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "nightdreambabe";
    public static string[] SupportedUrls => ["https://www.nightdreambabe.com/"];
    protected override string DirNameXpath => "//section[@class='outer-section']//h2[@class='section-title title']";
    protected override string ImageContainerXpath => "//div[@class='lightgallery thumbs quadruple fivefold']//a[@class='gallery-card']";

    public NightDreamBabeParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<NightDreamBabeParser>(filenameScheme))
    {
    }
}