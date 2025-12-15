using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using Core.Utility;
using OpenQA.Selenium;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class EightSeParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "8se";

    private const string CachePath = "eightsecache.json";

    public EightSeParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<EightSeParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for tw.8se.me and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse()
    {
        string dirName;
        List<string> imageLinks;
        List<string> videos;
        var usingCache = false;
        if (File.Exists(CachePath))
        {
            var cache = JsonUtility.Deserialize<EightSeCache>(CachePath);
            if (cache is not null)
            {
                Log.Information("Using cached data from {CachePath}", CachePath);
                (dirName, imageLinks, videos) = cache;
                usingCache = true;
            }
            else
            {
                (dirName, imageLinks, videos) = await ParseLinks();
            }
        }
        else
        {
            (dirName, imageLinks, videos) = await ParseLinks();
        }

        if (!usingCache)
        {
            var cache = new EightSeCache
            {
                DirName = dirName,
                Videos = videos,
                Links = imageLinks
            };

            JsonUtility.Serialize(CachePath, cache);
        }

        var images = new List<StringImageLinkWrapper>();
        foreach (var (i, link) in imageLinks.Enumerate())
        {
            Log.Information("Parsing image page {Link}", link);
            var soup = await Soupify(link, delay: 250);
            var img = soup.SelectSingleNodeOrThrow("//div[@class='container']/img").GetSrc();
            images.Add(img);
            if (i % 100 == 0 && i > 0)
            {
                await Sleep(2500); // Sleep every 100 images to avoid being rate-limited
            }
        }

        images.AddRange(videos.ToStringImageLinks());
        File.Delete(CachePath); // Clear the cache after parsing
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }

    private async Task<(string, List<string>, List<string>)> ParseLinks()
    {
        var id = CurrentUrl.Split("/")[4].Remove("id-").Remove(".html");
        var videos = await GetVideos();
        var soup = await Soupify();
        var dirName =
            soup.SelectSingleNodeOrThrow("//div[@class='info-card photo-detail']//div[@class='text']").InnerText +
            $" - ({id})";
        var imageLinks = new List<string>();
        while (true)
        {
            var imageContainer = soup.SelectSingleNode("//div[@class='list photo-items']") ??
                                 soup.SelectSingleNodeOrThrow("//div[@class='list amateur-items']");
            var links = imageContainer
                       .SelectNodesOrThrow(".//a")
                       .Select(a => "https://tw.8se.me" + a.GetHref());
            imageLinks.AddRange(links);
            var nextPageButton = soup.SelectSingleNode("//a[@class='pager-btn pager-next']");
            if (nextPageButton is null)
            {
                break;
            }

            var nextPageUrl = "https://tw.8se.me" + nextPageButton.GetHref();
            soup = await Soupify(nextPageUrl, delay: 250);
        }

        return (dirName, imageLinks, videos);
    }

    private async Task<List<string>> GetVideos()
    {
        var contentBox =
            Driver.TryFindElement(By.XPath("//div[@class='content-box']/div[@class='mp4-player-in-photo']"));
        if (contentBox is null)
        {
            return [];
        }

        var videos = new List<string>();
        var counter = 1;
        while (true)
        {
            Log.Information("Parsing video {Counter}", counter);
            counter++;
            var videoElement = contentBox.FindElement(By.TagName("video"));
            var src = videoElement.GetAttribute("src");
            if (!string.IsNullOrEmpty(src))
            {
                videos.Add(src);
            }
            else
            {
                Log.Warning("No video source found in the video element.");
            }

            var nextButton = contentBox.TryFindElement(By.XPath(".//div[@class='btn next']"));
            if (nextButton is null || nextButton.GetAttribute("disabled") is not null)
            {
                break;
            }

            //nextButton.Click();
            Driver.Click(nextButton);
            await Sleep(250); // Wait for the next video to load
        }

        return videos;
    }

    private class EightSeCache
    {
        public required string DirName { get; set; }
        public required List<string> Links { get; set; }
        public required List<string> Videos { get; set; }

        public void Deconstruct(out string dirName, out List<string> links, out List<string> videos)
        {
            dirName = DirName;
            links = Links;
            videos = Videos;
        }
    }
}