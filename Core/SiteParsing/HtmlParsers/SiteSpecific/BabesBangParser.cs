using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;

public class BabesBangParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "babesbang";
    
    protected override string DirNameXpath => "//div[@class='main-title']";
    protected override string ImageContainerXpath => "//div[@class='gal-block']";

    public BabesBangParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<BabesBangParser>(filenameScheme))
    {
    }
}