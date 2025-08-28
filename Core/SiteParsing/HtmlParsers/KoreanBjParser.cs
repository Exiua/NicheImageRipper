using Core.DataStructures;
using Core.Enums;
using Core.Exceptions;
using Core.ExtensionMethods;
using Core.SiteParsing.VideoCapturers;
using Core.Utility;
using OpenQA.Selenium;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class KoreanBjParser : HtmlParser
{
    private const string WaitForElementXPath = "//div[@id='responsive-player']/iframe|//video[@id='player']";
    private const string IframeXPath = "//div[@id='responsive-player']/iframe";
    private const string VideoXPath = "//video[@id='player']";
    
    public KoreanBjParser(WebDriver driver, Dictionary<string, string> requestHeaders,
                              FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders,
        filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for ww1.koreanbj.club and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var (capturer, b) = await ConfigureNetworkCapture<KoreanBjVideoCapturer>();
        await using var bidi = b;
        Driver.Refresh();
        var dirName = Driver.FindElement(By.XPath("//h2[@class='entry-title']")).Text;
        var waitedElement = await WaitForElement(WaitForElementXPath);
        var images = new List<StringImageLinkWrapper>();
        switch (waitedElement)
        {
            // if the iframe is loaded, we can immediately extract the video source as it's not a blob
            case "iframe":
                Log.Debug("Extracting video from iframe");
                await ExtractVideoFromIframe(images, capturer);
                break;
            // if the video tag is loaded, we can extract the source directly
            case "video":
            {
                Log.Debug("Extracting video from video tag");
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
                        Log.Debug("Extracting playlist from responsive player div");
                        await ExtractPlaylist(images, capturer);
                    }
                    else
                    {
                        throw new RipperException("Could not find responsive player div");
                    }
                }
                else
                {
                    Log.Debug("Extracting playlist from iframe");
                    await ExtractPlaylistFromIframe(images, capturer);
                }

                break;
            }
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }

    private async Task ExtractPlaylist(List<StringImageLinkWrapper> images, KoreanBjVideoCapturer capturer)
    {
        var i = 0;
        while (true)
        {
            var videos = capturer.GetNewVideoLinks();
            if (videos.Count == 0)
            {
                i++;
                await Sleep(250);
                if(i % 4 == 0)
                {
                    Log.Debug("Refreshing page to find video link");
                    Driver.Refresh();
                }
                
                continue;
            }

            var video = videos[0];
            Log.Debug("Found video URL: {url}", video);
            var filename = video.Split("/")[3] + ".mp4";
            var imageLink = new ImageLink(videos[0], FilenameScheme, 0, filename: filename)
            {
                LinkInfo = LinkInfo.M3U8YtDlp,
                Referer = "https://ww1.koreanbj.club/"
            };
            
            images.Add(imageLink);
            break;
        }
    }

    private async Task ExtractPlaylistFromIframe(List<StringImageLinkWrapper> images, KoreanBjVideoCapturer capturer)
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
        await WaitForElement(IframeXPath);
        var iframe = Driver.FindElement(By.XPath(IframeXPath));
        Driver.SwitchTo().Frame(iframe);
        await WaitForElement("//div[@id='a']");
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
                    Log.Warning("Video element not found, retrying...");
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
            
            await Sleep(500);
        }

        var i = 0;
        while (true)
        {
            i++;
            var videos = capturer.GetNewVideoLinks();
            if (videos.Count == 0)
            {
                await Sleep(250);
                if (i % 4 == 0)
                {
                    Log.Debug("Refreshing page to find video link");
                    Driver.Refresh();
                }
                
                continue;
            }

            var video = videos[0];
            Log.Debug("Found video URL: {url}", video);
            string url;
            if (video.Contains("%3F") || video.Contains("%3f"))
            {
                var encodedUrl = UrlUtility.GetUrlParameterValue(videos[0], "mu");
                url = Uri.UnescapeDataString(encodedUrl);
            }
            else
            {
                url = video;
            }
            
            var filename = UrlUtility.GetUrlParameterValue(url, "t") + ".mp4";
            var imageLink = new ImageLink(videos[0], FilenameScheme, 0, filename: filename)
            {
                LinkInfo = LinkInfo.M3U8YtDlp,
                Referer = "https://jilliandescribecompany.com/"
            };
            
            images.Add(imageLink);
            break;
        }
    }

    private async Task ExtractVideoFromIframe(List<StringImageLinkWrapper> images, KoreanBjVideoCapturer capturer)
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
                    await Sleep(250);
                    if (i % 4 == 0)
                    {
                        Log.Debug("Refreshing page to find video link");
                        Driver.Refresh();
                    }
                    
                    continue;
                }

                var video = videos[0];
                Log.Debug("Found video URL: {url}", video);
                var id = UrlUtility.GetUrlParameterValue(video, "pp");
                var filename = Uri.UnescapeDataString(id) + ".mp4";
                var imageLink = new ImageLink(video, FilenameScheme, 0, filename: filename)
                {
                    LinkInfo = LinkInfo.M3U8YtDlp,
                    Referer = iframeSrc
                };
            
                images.Add(imageLink);
                break;
            }
        }
        else
        {
            images.Add(videoSrc);
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
        return classAttribute is not null && (classAttribute.Contains("jw-state-playing") || classAttribute.Contains("jw-state-buffering"));
    }
}