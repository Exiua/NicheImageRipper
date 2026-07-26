using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing.VideoCapturers;
using NotSupportedException = NicheImageRipper.Core.Exceptions.NotSupportedException;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;

public class MissAvParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "missav";

    public MissAvParser(WebDriver driver, ApiClientManager apiClientManager, Dictionary<string, string> requestHeaders,
                  FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, apiClientManager,
        requestHeaders, IHtmlParser.GetFilenameScheme<MissAvParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the HTML for missav.ws and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var (capturer, b) = await ConfigureNetworkCapture<MissAvVideoCapturer>(cancellationToken);
        await using var bidi = b;
        Driver.Refresh();
        var soup = await Soupify(cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class]").InnerText;
        var images = new List<StringFileLinkWrapper>();
        if (CurrentUrl.Contains("/genres/"))
        {
            throw new NotSupportedException("GenreParsing", "Parsing genres is not supported for MissAv");
        }
        else
        {
            await WaitForPlaylist(capturer, links =>
            {
                var url = links[0];
                var filename = url.Split("/")[3] + ".mp4";
                var link = FileLink.WithFilename(url, filename, FilenameScheme, linkInfo: LinkInfo.M3U8YtDlp, referer: CurrentUrl);
                images.Add(link);
            }, cancellationToken);
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}