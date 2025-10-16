using System.Text.RegularExpressions;
using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Utility;
using HtmlAgilityPack;
using OpenQA.Selenium;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public partial class ArchivebateParser : HtmlParser
{
    private const string CachePath = "archivebateCache.json";
    
    public ArchivebateParser(WebDriver driver, Dictionary<string, string> requestHeaders,
                             FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders,
        filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for archivebate.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var verifyButton = Driver.FindElement(By.Id("verify"));
        Driver.Click(verifyButton);
        var soup = await Soupify();
        string dirName;
        var images = new List<StringImageLinkWrapper>();
        if (CurrentUrl.Contains("/profile/"))
        {
            dirName = soup.SelectSingleNodeOrThrow("//p[@class='mt-3 text-white mb-0']").InnerText;
            List<string> posts;
            if (File.Exists(CachePath))
            {
                Log.Information("Using cached post URLs from {CachePath}", CachePath);
                var temp = JsonUtility.Deserialize<List<string>>(CachePath);
                if (temp is null)
                {
                    posts = await GetPageUrls(soup);
                }
                else
                {
                    posts = temp;
                }
            }
            else
            {
                posts = await GetPageUrls(soup);
            }

            foreach (var (i, post) in posts.Enumerate())
            {
                Log.Information("Parsing post {Current} of {Total}: {Url}", i + 1, posts.Count, post);
                CurrentUrl = post;
                await Task.Delay(250);
                await WaitForElement("//iframe[@class='ab-rounded video-frame']");
                var url = await GetVideoUrl();
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
            var url = await GetVideoUrl();
            if (!url.IsNullOrEmpty())
            {
                images.Add(url);
            }
            
            Driver.SwitchTo().DefaultContent();
        }

        // Url used for the webdriver must be the same as the one used for downloading the video, otherwise it will 403
        return RipInfo.FromUrlList(images, dirName, FilenameScheme, referer: null);
    }

    private async Task<List<string>> GetPageUrls(HtmlNode soup)
    {
        Log.Information("Getting all post URLs");
        var posts = new List<string>();
        var pageCount = 1;
        while (true)
        {
            Log.Information("Parsing page {PageCount}", pageCount);
            pageCount++;
            var p = soup.SelectSingleNodeOrThrow("//div[@class='ab_grid']")
                        .SelectNodesOrThrow("./section")
                        .Select(section => section.SelectSingleNodeOrThrow(".//a").GetHref());
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
        
        JsonUtility.Serialize(CachePath, posts);

        return posts;
    }

    private async Task<string> GetVideoUrl()
    {
        var iframe = Driver.TryFindElement(By.XPath("//iframe[@class='ab-rounded video-frame']"));
        if (iframe is null)
        {
            var h4 = Driver.TryFindElement(By.XPath("//h4"));
            if (h4?.Text.Contains("This video has been") ?? false)
            {
                Log.Information("Video has been deleted");
                return "";
            }
            
            Log.Warning("Iframe not found, video probably unavailable");
            return "";
        }
        
        Driver.SwitchTo().Frame(iframe);
        var overlays = Driver.FindElements(By.XPath("//div[not(@class) and @style and not(@id)]"));
        foreach(var overlay in overlays)
        {
            Driver.RemoveElement(overlay);
        }
            
        await Task.Delay(250);
        var playButton = Driver.TryFindElement(By.XPath("//button[@class='vjs-big-play-button']"));
        if (playButton is null)
        {
            var h2 = Driver.TryFindElement(By.XPath("//h2"));
            if (h2?.Text.Contains("WE ARE SORRY") ?? false)
            {
                Log.Information("Video is unavailable");
                return "";
            }
            
            Log.Warning("Play button not found, video probably unavailable");
            return "";
        }
        
        playButton.Click();
        await Task.Delay(250);
        var video = Driver.FindElement(By.XPath("//video[@src]"));
        var url = video.GetAttribute("src")!;
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