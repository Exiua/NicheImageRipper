
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using NicheImageRipper.SiteModules.Modules.Generic;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.ErosBerry;
public class ErosBerryParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "erosberry";
    public static string[] SupportedUrls => ["https://www.erosberry.com/"];
    protected override string DirNameXpath => "//h1[@class='title']";
    protected override string ImageContainerXpath => "//div[@class='block-post three-post flex']";

    public ErosBerryParser(WebDriver driver,  Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver,  requestHeaders, IHtmlParser.GetFilenameScheme<ErosBerryParser>(filenameScheme))
    {
    }
}