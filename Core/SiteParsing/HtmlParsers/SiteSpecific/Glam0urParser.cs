using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;

// glam0ur.com
public class Glam0urParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "glam0ur";
    
    protected override string DirNameXpath => "//div[@class='picnav']//h1";
    protected override string ImageContainerXpath => "//div[@class='center']/a";

    public Glam0urParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders,
                         FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager,
        requestHeaders, IHtmlParser.GetFilenameScheme<Glam0urParser>(filenameScheme))
    {
    }
}