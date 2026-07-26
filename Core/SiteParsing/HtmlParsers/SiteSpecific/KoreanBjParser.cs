using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Exceptions;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing.VideoCapturers;
using NicheImageRipper.Core.Utility;
using OpenQA.Selenium;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;

public class KoreanBjParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "koreanbj";

    private const string WaitForElementXPath = "//div[@id='responsive-player']/iframe|//video[@id='player']";
    private const string IframeXPath = "//div[@id='responsive-player']/iframe";
    private const string VideoXPath = "//video[@id='player']";

    public KoreanBjParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders,
                          FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager,
        requestHeaders, IHtmlParser.GetFilenameScheme<KoreanBjParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the HTML for ww1.koreanbj.club and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var (capturer, b) = await ConfigureNetworkCapture<KoreanBjVideoCapturer>(cancellationToken);
        await using var bidi = b;
        Driver.Refresh();
        var dirName = Driver.FindElement(By.XPath("//h2[@class='entry-title']")).Text;
        var waitedElement = await WaitForElement(WaitForElementXPath, cancellationToken: cancellationToken);
        var images = new List<StringFileLinkWrapper>();
        switch (waitedElement)
        {
            // if the iframe is loaded, we can immediately extract the video source as it's not a blob
            case "iframe":
                Logger.Debug("Extracting video from iframe");
                await ExtractVideoFromIframe(images, capturer, cancellationToken);
                break;
            // if the video tag is loaded, we can extract the source directly
            case "video":
            {
                Logger.Debug("Extracting video from video tag");
                var player = Driver.FindElement(By.XPath(VideoXPath));
                var source = player.FindElement(By.XPath("./source"));
                var url = source.GetSrc();
                images.Add(url);
                break;
            }
            // if neither is loaded, we have to capture the playlist over network
            default:
            {
                var responsivePlayer = Driver.TryFindElement(By.XPath("//div[@id='responsive-player']"));
                if (responsivePlayer is null)
                {
                    responsivePlayer = Driver.TryFindElement(By.XPath("//div[@class='responsive-player']"));
                    if (responsivePlayer is not null)
                    {
                        Logger.Debug("Extracting playlist from responsive player div");
                        await ExtractPlaylist(images, capturer, cancellationToken);
                    }
                    else
                    {
                        throw new RipperException("Could not find responsive player div");
                    }
                }
                else
                {
                    Logger.Debug("Extracting playlist from iframe");
                    await ExtractPlaylistFromIframe(images, capturer, cancellationToken);
                }

                break;
            }
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }

    private async Task ExtractPlaylist(List<StringFileLinkWrapper> files, KoreanBjVideoCapturer capturer,
                                       CancellationToken cancellationToken = default)
    {
        var i = 0;
        while (true)
        {
            var videos = capturer.GetNewVideoLinks();
            if (videos.Count == 0)
            {
                i++;
                await Sleep(250, cancellationToken);
                if (i % 4 == 0)
                {
                    Logger.Debug("Refreshing page to find video link");
                    Driver.Refresh();
                }

                continue;
            }

            var video = videos[0];
            Logger.Debug("Found video URL: {url}", video);
            var filename = video.Split("/")[3] + ".mp4";
            var fileLink = FileLink.WithFilename(videos[0], filename, FilenameScheme, linkInfo: LinkInfo.M3U8YtDlp,
                referer: "https://ww1.koreanbj.club/");

            files.Add(fileLink);
            break;
        }
    }

    private async Task ExtractPlaylistFromIframe(List<StringFileLinkWrapper> files, KoreanBjVideoCapturer capturer,
                                                 CancellationToken cancellationToken = default)
    {
        var closeButton = Driver.TryFindElement(By.XPath("//button[normalize-space(.)='Close']"));
        if (closeButton is not null)
        {
            try
            {
                closeButton.Click();
            }
            catch (ElementNotInteractableException)
            {
                Driver.Click(closeButton);
            }
        }

        var playButton = Driver.TryFindElement(By.XPath("//div[@id='play-button']"));
        playButton?.Click();
        await WaitForElement(IframeXPath, cancellationToken: cancellationToken);
        var iframe = Driver.FindElement(By.XPath(IframeXPath));
        Driver.SwitchTo().Frame(iframe);
        await WaitForElement("//div[@id='a']", cancellationToken: cancellationToken);
        var videoElement = Driver.FindElement(By.XPath("//div[@id='a']"));
        videoElement.Click();
        while (!VideoIsPlaying())
        {
            CleanTabs("koreanbj.club");
            videoElement = Driver.TryFindElement(By.XPath("//div[@id='a']"));
            if (videoElement is null)
            {
                Driver.SwitchTo().DefaultContent();
                iframe = Driver.FindElement(By.XPath(IframeXPath));
                Driver.SwitchTo().Frame(iframe);
                videoElement = Driver.TryFindElement(By.XPath("//div[@id='a']"));
                if (videoElement is null)
                {
                    Logger.Warning("Video element not found, retrying...");
                }
                else
                {
                    videoElement.Click();
                }
            }
            else
            {
                videoElement.Click();
            }

            await Sleep(500, cancellationToken);
        }

        var i = 0;
        while (true)
        {
            i++;
            var videos = capturer.GetNewVideoLinks();
            if (videos.Count == 0)
            {
                await Sleep(250, cancellationToken);
                if (i % 4 == 0)
                {
                    Logger.Debug("Refreshing page to find video link");
                    Driver.Refresh();
                }

                continue;
            }

            var video = videos[0];
            Logger.Debug("Found video URL: {url}", video);
            string url;
            if (video.Contains("%3F") || video.Contains("%3f"))
            {
                Logger.Debug("Detected encoded URL, decoding");
                var encodedUrl = UrlUtility.GetUrlParameterValue(videos[0], "mu");
                url = Uri.UnescapeDataString(encodedUrl);
                Logger.Debug("Decoded URL: {url}", url);
            }
            else
            {
                url = video;
            }

            var filename = UrlUtility.GetUrlParameterValue(url, "t") + ".mp4";
            var fileLink = FileLink.WithFilename(url, filename, FilenameScheme, linkInfo: LinkInfo.M3U8YtDlp,
                referer: "https://jilliandescribecompany.com/");

            files.Add(fileLink);
            break;
        }
    }

    private async Task ExtractVideoFromIframe(List<StringFileLinkWrapper> files, KoreanBjVideoCapturer capturer,
                                              CancellationToken cancellationToken = default)
    {
        var iframe = Driver.FindElement(By.XPath(IframeXPath));
        var iframeSrc = iframe.GetAttribute("src")!;
        Driver.SwitchTo().Frame(iframe);
        var videoElement = Driver.FindElement(By.XPath("//video"));
        var videoSrc = videoElement.GetAttribute("src")!;
        if (videoSrc.StartsWith("blob:"))
        {
            var i = 0;
            while (true)
            {
                i++;
                var videos = capturer.GetNewVideoLinks();
                if (videos.Count == 0)
                {
                    await Sleep(250, cancellationToken);
                    if (i % 4 == 0)
                    {
                        Logger.Debug("Refreshing page to find video link");
                        Driver.Refresh();
                    }

                    continue;
                }

                var video = videos[0];
                Logger.Debug("Found video URL: {url}", video);
                var id = UrlUtility.GetUrlParameterValue(video, "pp");
                var filename = Uri.UnescapeDataString(id) + ".mp4";
                var fileLink = FileLink.WithFilename(video, filename, FilenameScheme, linkInfo: LinkInfo.M3U8YtDlp,
                    referer: iframeSrc);

                files.Add(fileLink);
                break;
            }
        }
        else
        {
            files.Add(videoSrc);
        }
    }

    private bool VideoIsPlaying()
    {
        var videoElement = Driver.TryFindElement(By.XPath("//div[@id='a']"));
        if (videoElement == null)
        {
            return false;
        }

        var classAttribute = videoElement.GetAttribute("class");
        return classAttribute is not null &&
               (classAttribute.Contains("jw-state-playing") || classAttribute.Contains("jw-state-buffering"));
    }
}