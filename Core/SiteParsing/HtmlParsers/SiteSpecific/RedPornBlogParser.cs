using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;

// redpornblog.com
public class RedPornBlogParser : GenericBabesGalleryParser, IHtmlParser
{
    public static string ParserName => "redpornblog";

    protected override string DirNameXpath => "//div[@id='pic-title']//h1";
    protected override string ImageContainerXpath => "//div[@id='bigpic-image']";

    public RedPornBlogParser(WebDriver driver, ApiClientManager clientManager,
                             Dictionary<string, string> requestHeaders,
                             FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager,
        requestHeaders, IHtmlParser.GetFilenameScheme<RedPornBlogParser>(filenameScheme))
    {
    }
}