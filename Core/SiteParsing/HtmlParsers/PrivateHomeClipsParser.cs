using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing.VideoCapturers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers;

public class PrivateHomeClipsParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "privatehomeclips";

    public PrivateHomeClipsParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<PrivateHomeClipsParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for privatehomeclips.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var (capturer, b) = await ConfigureNetworkCapture<PrivateHomeClipsVideoCapturer>();
        await using var bidi = b;
        Driver.Refresh();
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='video-page__title']").ChildNodes[0].InnerText;
        var images = new List<StringImageLinkWrapper>();
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
                
                var link = new ImageLink(url, FilenameScheme, 0, filename: filename)
                {
                    LinkInfo = LinkInfo.M3U8YtDlp,
                    Referer = CurrentUrl
                };
                
                images.Add(link);
            });
        }
        else
        {
            images.Add("https://privatehomeclips.com" + videoUrl);
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}