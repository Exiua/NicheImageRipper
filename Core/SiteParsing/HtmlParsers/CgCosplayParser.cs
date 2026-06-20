using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing.VideoCapturers;
using NicheImageRipper.Core.Utility;
using OpenQA.Selenium;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers;

public class CgCosplayParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "cgcosplay";

    public CgCosplayParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<CgCosplayParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for cgcosplay.org and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(lazyLoadArgs: new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250
        });
        var dirName = soup.SelectSingleNodeOrThrow("//h2[@class='elementor-heading-title elementor-size-xxl']").InnerText;
        var images = soup.SelectSingleNodeOrThrow("//div[@id='gallery-1']")
                            .SelectNodesOrThrow("./figure")
                            .Select(fig => fig.SelectSingleNodeOrThrow(".//img").GetSrc())
                            .ToStringImageLinkWrapperList();
        var videos = soup.SelectSingleNode("//main[@id='main']");
        if (videos is not null)
        {
            var videoRawLinks = videos.SelectNodesOrThrow(".//*[self::iframe or self::video]")
                                        // .Select(div =>
                                        //      div.SelectSingleNode(".//video") ?? div.SelectSingleNode(".//iframe"))
                                        .Select(elm => elm.GetSrc());
            var captures = new Dictionary<string, PlaylistCapturer>();
            foreach (var (i, link) in videoRawLinks.Enumerate())
            {
                var cleanLink = link.DecodeUrl();
                Logger.Debug("Video {index}: {link}", i + 1, cleanLink);
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
                    Logger.Debug("Suppressed spam link");
                    // suppress, it's just spam
                }
                else
                {
                    Logger.Warning("Link not handled: {link}", cleanLink);
                }
            }
        }
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    
        async Task<string> ResolveVkLink(string url, PlaylistCapturer capturer)
        {
            CurrentUrl = url;
            await Sleep(250);
            while (true)
            {
                if (CurrentUrl.Contains("autoplay=1"))
                {
                    break;
                }
                
                var playButton = Driver.TryFindElement(By.XPath("//div[@class='videoplayer_thumb']"));
                if (playButton is null)
                {
                    Logger.Debug("Play button not found, retrying...");
                    Driver.TakeDebugScreenshot();
                    await Sleep(1000);
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
                    Logger.Debug("No links found, retrying...");
                    await Sleep(1000);
                    continue;
                }
                
                break;
            }
    
            return links[0];
        }
    }
}
