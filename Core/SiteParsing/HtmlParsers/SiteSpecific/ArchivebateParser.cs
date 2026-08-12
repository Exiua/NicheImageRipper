using System.Text.RegularExpressions;
using HtmlAgilityPack;
using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.Utility;
using OpenQA.Selenium;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;
public partial class ArchivebateParser : HtmlParser, IHtmlParser
{
    private const string CachePath = "archivebateCache.json";
    private const int MaxConcurrentDownloads = 30;
    public static string ParserName => "archivebate";
    public static string[] SupportedUrls => ["https://www.archivebate.com/", "https://archivebate.com/", "https://archivebate.cc/"];

    public ArchivebateParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<ArchivebateParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the HTML for archivebate.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var verifyButton = Driver.FindElement(By.Id("verify"));
        Driver.Click(verifyButton);
        var soup = await Soupify(cancellationToken: cancellationToken);
        string dirName;
        var images = new List<StringFileLinkWrapper>();
        if (CurrentUrl.Contains("/profile/"))
        {
            var profileName = CurrentUrl.Split("/")[4];
            soup = await Soupify(xpath: "//p[@class='mt-3 text-white mb-0']", cancellationToken: cancellationToken);
            dirName = soup.SelectSingleNodeOrThrow("//p[@class='mt-3 text-white mb-0']").InnerText;
            List<string> posts;
            if (File.Exists(CachePath))
            {
                Logger.Information("Using cached post URLs from {CachePath}", CachePath);
                var temp = JsonUtility.Deserialize<Dictionary<string, List<string>>>(CachePath);
                if (temp is null)
                {
                    posts = await GetPageUrls(soup, profileName);
                }
                else
                {
                    if (temp.TryGetValue(profileName, out var p))
                    {
                        posts = p;
                    }
                    else
                    {
                        posts = await GetPageUrls(soup, profileName);
                    }
                }
            }
            else
            {
                posts = await GetPageUrls(soup, profileName);
            }

            foreach (var(i, post)in posts.Enumerate())
            {
                Logger.Information("Parsing post {Current} of {Total}: {Url}", i + 1, posts.Count, post);
                CurrentUrl = post;
                await Task.Delay(250, cancellationToken);
                await WaitForElement("//iframe[@class='ab-rounded video-frame']", cancellationToken: cancellationToken);
                var url = await GetVideoUrl(cancellationToken);
                if (!url.IsNullOrEmpty())
                {
                    images.Add(url);
                }
            }
        }
        else
        {
            var title = soup.SelectSingleNodeOrThrow("//div[@class='info']/p").ChildNodes[0].InnerText;
            title = CompactSpaces(title);
            var performer = soup.SelectSingleNodeOrThrow("//div[@class='info d-flex align-items-center']//a").InnerText;
            dirName = $"{performer} - {title}";
            var url = await GetVideoUrl(cancellationToken);
            if (!url.IsNullOrEmpty())
            {
                images.Add(url);
            }

            Driver.SwitchTo().DefaultContent();
        }

        // Url used for the webdriver must be the same as the one used for downloading the video, otherwise it will 403
        return RipInfo.FromUrlList(images, dirName, FilenameScheme, referer: null, maxConcurrentDownloads: MaxConcurrentDownloads);
    }

    private async Task<List<string>> GetPageUrls(HtmlNode soup, string profileName)
    {
        Logger.Information("Getting all post URLs");
        var posts = new List<string>();
        var pageCount = 1;
        while (true)
        {
            Logger.Information("Parsing page {PageCount}", pageCount);
            pageCount++;
            var p = soup.SelectSingleNodeOrThrow("//div[@class='ab_grid']").SelectNodesOrThrow("./section").Select(section => section.SelectSingleNodeOrThrow(".//a").GetHref());
            posts.AddRange(p);
            var pagination = soup.SelectSingleNode("//div[not(@class)]/ul[@class='pagination']");
            if (pagination is null)
            {
                break;
            }

            var nextButton = pagination.SelectNodesOrThrow("./li")[^1];
            var nextButtonClass = nextButton.GetAttributeValue("class");
            if (nextButtonClass.Contains("disabled"))
            {
                break;
            }

            var nextLink = nextButton.SelectSingleNodeOrThrow("./a").GetHref();
            soup = await Soupify(nextLink, xpath: "//div[@class='ab_grid']/section//a");
        }

        var cache = new Dictionary<string, List<string>>
        {
            [profileName] = posts
        };
        JsonUtility.Serialize(CachePath, cache);
        return posts;
    }

    private async Task<string> GetVideoUrl(CancellationToken cancellationToken = default)
    {
        var iframe = Driver.TryFindElement(By.XPath("//iframe[@class='ab-rounded video-frame']"));
        if (iframe is null)
        {
            var h4 = Driver.TryFindElement(By.XPath("//h4"));
            if (h4?.Text.Contains("This video has been") ?? false)
            {
                Logger.Information("Video has been deleted");
                return "";
            }

            Logger.Warning("Iframe not found, video probably unavailable");
            return "";
        }

        Driver.SwitchTo().Frame(iframe);
        while (true)
        {
            try
            {
                var overlays = Driver.FindElements(By.XPath("//div[not(@class) and @style and not(@id)]"));
                foreach (var overlay in overlays)
                {
                    Driver.RemoveElement(overlay);
                }

                break;
            }
            catch (StaleElementReferenceException)
            {
                await Sleep(250, cancellationToken);
            }
        }

        const int maxAttempts = 4;
        await Task.Delay(250, cancellationToken);
        IWebElement? video = null;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var playButton = Driver.TryFindElement(By.XPath("//button[@class='vjs-big-play-button']"));
            if (playButton is null)
            {
                var h2 = Driver.TryFindElement(By.XPath("//h2"));
                if (h2?.Text.Contains("WE ARE SORRY") ?? false)
                {
                    Logger.Information("Video is unavailable");
                    return "";
                }

                var videoJs = Driver.TryFindElement(By.XPath("//div[@id='videojs']"));
                if (videoJs is null)
                {
                    if (attempt == maxAttempts - 1)
                    {
                        Logger.Warning("VideoJS container not found, video probably unavailable");
                        return "";
                    }

                    await Sleep(1000, cancellationToken);
                    continue;
                }

                var classes = videoJs.GetAttribute("class")!;
                if (!classes.Contains("vjs-playing"))
                {
                    if (attempt == maxAttempts - 1)
                    {
                        Logger.Warning("Play button not found, video probably unavailable");
                        return "";
                    }

                    await Sleep(1000, cancellationToken);
                    continue;
                }
            // Video is already playing
            }
            else
            {
                try
                {
                    playButton.Click();
                }
                catch (ElementNotInteractableException)
                {
                    if (attempt == maxAttempts - 1)
                    {
                        throw;
                    }

                    Driver.ScrollElementIntoView(playButton);
                    await Sleep(5000, cancellationToken);
                    continue;
                }
            }

            await Task.Delay(250, cancellationToken);
            video = Driver.TryFindElement(By.XPath("//video[@src]"));
            if (video is not null)
            {
                break;
            }

            if (attempt == maxAttempts - 1)
            {
                Logger.Warning("Video element not found, video probably unavailable");
                return "";
            }

            await Sleep(1000, cancellationToken);
        }

        var url = video!.GetAttribute("src")!;
        Driver.SwitchTo().DefaultContent();
        return url;
    }

    private static string CompactSpaces(string input)
    {
        input = input.Trim();
        return MultiSpaceRegex().Replace(input, " ");
    }

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex MultiSpaceRegex();
}