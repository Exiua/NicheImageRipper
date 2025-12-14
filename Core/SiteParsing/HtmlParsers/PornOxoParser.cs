using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using Core.SiteParsing.VideoCapturers;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class PornOxoParser : HtmlParser
{
    public PornOxoParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for pornoxo.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse()
    {
        var id = CurrentUrl.Split("/")[4];
        var (capturer, b) = await ConfigureNetworkCapture<PornOxoCapturer>();
        await using var bidi = b;
        Driver.Refresh();
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//div[@class='video-top-header']/h1").ChildNodes[0].InnerText.Trim() + $" ({id})";
        var images = new List<StringImageLinkWrapper>();
        await WaitForPlaylist(capturer, links =>
        {
            var link = new ImageLink(links[0], FilenameScheme, 0)
            {
                LinkInfo = LinkInfo.M3U8Ffmpeg
            };
            images.Add(link);
        } );

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}