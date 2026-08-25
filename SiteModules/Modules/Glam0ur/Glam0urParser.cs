
using NicheImageRipper.SiteModules.Modules.Generic;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.Glam0ur;
// glam0ur.com
public class Glam0urParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "glam0ur";
    public static string[] SupportedUrls => ["https://www.glam0ur.com/"];
    protected override string DirNameXpath => "//div[@class='picnav']//h1";
    protected override string ImageContainerXpath => "//div[@class='center']/a";

    public Glam0urParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<Glam0urParser>(filenameScheme))
    {
    }
}