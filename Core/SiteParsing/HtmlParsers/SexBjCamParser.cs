using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing.VideoCapturers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers;

public class SexBjCamParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "sexbjcam";

    public SexBjCamParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders,
                          FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager,
        requestHeaders, IHtmlParser.GetFilenameScheme<SexBjCamParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for sexbjcam.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await SolveParseAddCookies(regenerateSessionOnFailure: true, cancellationToken: cancellationToken);
        //Driver.TakeDebugScreenshot();
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='entry-title']").InnerText;
        var iframe = soup.SelectSingleNodeOrThrow("//iframe[@allowfullscreen]");
        var iframeUrl = iframe.GetSrc();
        Logger.Debug("Navigating to iframe URL: {IframeUrl}", iframeUrl);
        var (capturer, b) = await ConfigureNetworkCapture<SexBjCamVideoCapturer>(cancellationToken);
        await using var bidi = b;
        CurrentUrl = iframeUrl;
        var referer = iframeUrl.Split("/")[..3].Join("/") + '/';
        StringFileLinkWrapper playlist;
        while (true)
        {
            var links = capturer.GetNewVideoLinks();
            if (links.Count == 0)
            {
                continue;
            }

            playlist = FileLink.WithFilename(links[0], "playlist.m3u8", FilenameScheme, linkInfo: LinkInfo.M3U8Ffmpeg,
                referer: referer);
            break;
        }

        return RipInfo.FromUrlList([playlist], dirName, FilenameScheme);
    }
}