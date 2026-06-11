using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using Core.SiteParsing.VideoCapturers;
using OpenQA.Selenium;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class BestCamParser : HtmlParser
{
    public BestCamParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for bestcam.tv and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var (capturer, b) = await ConfigureNetworkCapture<BestCamVideoCapturer>();
        await using var bidi = b;
        var soup = await SolveParse();
        string dirName;
        var images = new List<StringImageLinkWrapper>();
        if (CurrentUrl.Contains("/model/"))
        {
            dirName = soup.SelectSingleNodeOrThrow("//div[@class='actor-name']/h1").InnerText;
        }
        else
        {
            dirName = soup.SelectSingleNodeOrThrow("//h1[@class='movie-detail-name']").InnerText;
            var playButton = Driver.FindElement(By.XPath("//div[@class='play-icon']"));
            playButton.Click();
            await WaitForPlaylist(capturer, links =>
            {
                var url = links[0];
                var filename = url.Split("/")[4].Split("?")[0].Remove(".m3u8") + ".mp4";
                var link = new ImageLink(url, FilenameScheme, 0, filename: filename)
                {
                    LinkInfo = LinkInfo.M3U8Ffmpeg,
                    Referer = CurrentUrl
                };
                images.Add(link);
            });
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}