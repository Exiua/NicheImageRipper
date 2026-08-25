
using NicheImageRipper.SiteModules.Modules.Generic;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.ErosBerry;
public class ErosBerryParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "erosberry";
    public static string[] SupportedUrls => ["https://www.erosberry.com/"];
    protected override string DirNameXpath => "//h1[@class='title']";
    protected override string ImageContainerXpath => "//div[@class='block-post three-post flex']";

    public ErosBerryParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<ErosBerryParser>(filenameScheme))
    {
    }
}