using Core.DataStructures;
using Core.DataStructures.VideoCapturers;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Utility;
using OpenQA.Selenium;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class CgCosplayParser : HtmlParser
{
    public CgCosplayParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for cgcosplay.org and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify(lazyLoadArgs: new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250
        });
        var dirName = soup.SelectSingleNode("//h2[@class='elementor-heading-title elementor-size-xxl']").InnerText;
        var images = soup.SelectSingleNode("//div[@id='gallery-1']")
                            .SelectNodes("./figure")
                            .Select(fig => fig.SelectSingleNode(".//img").GetSrc())
                            .ToStringImageLinkWrapperList();
        var videos = soup.SelectSingleNode("//main[@id='main']");
        if (videos is not null)
        {
            var videoRawLinks = videos.SelectNodes(".//*[self::iframe or self::video]")
                                        // .Select(div =>
                                        //      div.SelectSingleNode(".//video") ?? div.SelectSingleNode(".//iframe"))
                                        .Select(elm => elm.GetSrc());
            var captures = new Dictionary<string, PlaylistCapturer>();
            foreach (var (i, link) in videoRawLinks.Enumerate())
            {
                var cleanLink = link.DecodeUrl();
                Log.Debug("Video {index}: {link}", i + 1, cleanLink);
                if (cleanLink.Contains("cgcosplay.org"))
                {
                    images.Add(cleanLink);
                }
                else if (cleanLink.Contains("vk.com"))
                {
                    if (!captures.TryGetValue("vk.com", out var capturer))
                    {
                        (capturer, _) = await ConfigureNetworkCapture<VkVideoCapturer>();
                        captures.Add("vk.com", capturer);
                    }
                    
                    var resolvedLink = await ResolveVkLink(cleanLink, capturer);
                    images.Add(resolvedLink);
                }
                else if (cleanLink.Contains("youtube.com"))
                {
                    images.Add(cleanLink);
                }
                else if (cleanLink.Contains("late-anxiety.com"))
                {
                    Log.Debug("Suppressed spam link");
                    // suppress, it's just spam
                }
                else
                {
                    Log.Warning("Link not handled: {link}", cleanLink);
                }
            }
        }
    
        return new RipInfo(images, dirName, FilenameScheme);
    
        async Task<string> ResolveVkLink(string url, PlaylistCapturer capturer)
        {
            CurrentUrl = url;
            await Task.Delay(250);
            while (true)
            {
                if (CurrentUrl.Contains("autoplay=1"))
                {
                    break;
                }
                
                var playButton = Driver.TryFindElement(By.XPath("//div[@class='videoplayer_thumb']"));
                if (playButton is null)
                {
                    Log.Debug("Play button not found, retrying...");
                    Driver.TakeDebugScreenshot();
                    await Task.Delay(1000);
                    continue;
                }
    
                playButton.Click();
                break;
            }
    
            List<string> links;
            while (true)
            {
                links = capturer.GetNewVideoLinks();
                if (links.Count == 0)
                {
                    Log.Debug("No links found, retrying...");
                    await Task.Delay(1000);
                    continue;
                }
                
                break;
            }
    
            return links[0];
        }
    }
}
