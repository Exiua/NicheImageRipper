
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using NicheImageRipper.SiteModules.Modules.Generic;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.SexyBabesArt;
public class SexyBabesArtParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "sexybabesart";
    public static string[] SupportedUrls => ["https://www.sexybabesart.com/"];
    protected override string DirNameXpath => "//div[@class='content-title']/h1";
    protected override string ImageContainerXpath => "//div[@class='thumbs']";

    public SexyBabesArtParser(WebDriver driver,  Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver,  requestHeaders, IHtmlParser.GetFilenameScheme<SexyBabesArtParser>(filenameScheme))
    {
    }
}