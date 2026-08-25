

using Sdk.DataStructures;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.PrivateHomeClips;
public class PrivateHomeClipsParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "privatehomeclips";
    public static string[] SupportedUrls => ["https://privatehomeclips.com/"];

    public PrivateHomeClipsParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<PrivateHomeClipsParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the HTML for privatehomeclips.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var(capturer, b) = await ConfigureNetworkCapture<PrivateHomeClipsVideoCapturer>(cancellationToken);
        await using var bidi = b;
        Driver.Refresh();
        var soup = await Soupify(cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='video-page__title']").ChildNodes[0].InnerText;
        var images = new List<StringFileLinkWrapper>();
        var videoUrl = soup.SelectSingleNodeOrThrow("//video").GetSrc();
        if (videoUrl.StartsWith("blob:"))
        {
            await WaitForPlaylist(capturer, links =>
            {
                var url = links[0];
                string filename;
                if (url.Contains(".mp4.m3u8"))
                {
                    // Get the last part of the URL and remove the .m3u8 extension
                    filename = url.Split("/")[^1][..^5];
                }
                else
                {
                    filename = url.Split("/")[^2];
                }

                var link = FileLink.WithFilename(url, filename, FilenameScheme, linkInfo: LinkInfo.M3U8YtDlp, referer: CurrentUrl);
                images.Add(link);
            }, cancellationToken);
        }
        else
        {
            images.Add("https://privatehomeclips.com" + videoUrl);
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}