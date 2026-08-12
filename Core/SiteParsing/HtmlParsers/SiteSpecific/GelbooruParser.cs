using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;
public class GelbooruParser : BooruParser, IHtmlParser
{
    public static string ParserName => "gelbooru";
    public static string[] SupportedUrls => ["https://gelbooru.com/"];

    public GelbooruParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<GelbooruParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for gelbooru.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        return BooruParse(Booru.Gelbooru);
    }
}