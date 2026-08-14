using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.SiteParsing.HtmlParsers.SiteSpecific;
public class ArcaParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "arca";
    public static string[] SupportedUrls => ["https://arca.live/"];

    public ArcaParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<ArcaParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for arca.live and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//div[@class='title']").InnerText;
        var mainTag = soup.SelectSingleNodeOrThrow("//div[@class='fr-view article-content']");
        var images = new List<StringFileLinkWrapper>();
        var imageList = mainTag.SelectNodesSafe(".//img").GetSrcs();
        var imgs = imageList.Select(image => image.Split("?")[0] + "?type=orig") // Remove query string and add type=orig
        .Select(img => !img.Contains(Protocol) ? Protocol + img : img) // Add protocol if missing
        .Select(dummy => (StringFileLinkWrapper)dummy) // Convert to StringImageLinkWrapper
        .ToList();
        images.AddRange(imgs);
        var videoList = mainTag.SelectNodesSafe(".//video").GetSrcs();
        var videos = videoList.Select(video => !video.Contains(Protocol) ? Protocol + video : video).Select(dummy => (StringFileLinkWrapper)dummy).ToList();
        images.AddRange(videos);
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}