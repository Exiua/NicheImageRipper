using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.SiteParsing.HtmlParsers.SiteSpecific;
public class XHamsterParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "xhamster";
    public static string[] SupportedUrls => ["https://xhamster.com/"];

    public XHamsterParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<XHamsterParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the HTML for xhamster.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var id = CurrentUrl.Split("-")[^1].Split("?")[0];
        var(capturer, b) = await ConfigureNetworkCapture<XHamsterCapturer>(cancellationToken);
        await using var bidi = b;
        Driver.Refresh();
        var soup = await Soupify(cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//h1").InnerText + $" ({id})";
        var images = new List<StringFileLinkWrapper>();
        await WaitForPlaylist(capturer, links =>
        {
            var url = links[0];
            var filename = UrlUtility.GetUrlParameterValue(url, "key").Split(",")[0] + ".mp4";
            var link = FileLink.WithFilename(url, filename, FilenameScheme, linkInfo: LinkInfo.M3U8Ffmpeg);
            images.Add(link);
        }, cancellationToken);
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}