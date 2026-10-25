
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using NicheImageRipper.SiteModules.Modules.Generic;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.RedPornBlog;
// redpornblog.com
public class RedPornBlogParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "redpornblog";
    public static string[] SupportedUrls => ["https://www.redpornblog.com/"];
    protected override string DirNameXpath => "//div[@id='pic-title']//h1";
    protected override string ImageContainerXpath => "//div[@id='bigpic-image']";

    public RedPornBlogParser(WebDriver driver,  Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver,  requestHeaders, IHtmlParser.GetFilenameScheme<RedPornBlogParser>(filenameScheme))
    {
    }
}