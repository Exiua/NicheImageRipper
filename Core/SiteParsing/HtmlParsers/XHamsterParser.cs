using Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing.VideoCapturers;
using NicheImageRipper.Core.Utility;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers;

public class XHamsterParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "xhamster";

    public XHamsterParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<XHamsterParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for xhamster.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var id = CurrentUrl.Split("-")[^1].Split("?")[0];
        var (capturer, b) = await ConfigureNetworkCapture<XHamsterCapturer>();
        await using var bidi = b;
        Driver.Refresh();
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//h1").InnerText + $" ({id})";
        var images = new List<StringImageLinkWrapper>();
        await WaitForPlaylist(capturer, links =>
        {
            var url = links[0];
            var filename = UrlUtility.GetUrlParameterValue(url, "key").Split(",")[0] + ".mp4";
            var link = new ImageLink(url, FilenameScheme, 0, filename: filename)
            {
                LinkInfo = LinkInfo.M3U8Ffmpeg
            };
            images.Add(link);
        });

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}