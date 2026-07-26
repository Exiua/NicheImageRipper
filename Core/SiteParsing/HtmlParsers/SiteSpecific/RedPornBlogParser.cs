using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;

public class RedPornBlogParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "redpornblog";

    public RedPornBlogParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<RedPornBlogParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for redpornblog.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        return GenericBabesHtmlParser("//div[@id='pic-title']//h1", "//div[@id='bigpic-image']");
    }
}
