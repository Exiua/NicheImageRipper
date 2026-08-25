
using NicheImageRipper.SiteModules.Modules.Generic;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.RedPornBlog;
// redpornblog.com
public class RedPornBlogParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "redpornblog";
    public static string[] SupportedUrls => ["https://www.redpornblog.com/"];
    protected override string DirNameXpath => "//div[@id='pic-title']//h1";
    protected override string ImageContainerXpath => "//div[@id='bigpic-image']";

    public RedPornBlogParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<RedPornBlogParser>(filenameScheme))
    {
    }
}