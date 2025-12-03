using Common.ExtensionMethods;
using System.Text.Json;
using System.Text.Json.Nodes;
using Core.DataStructures;
using Core.Enums;
using Core.Exceptions;
using Core.ExtensionMethods;
using Core.Managers;
using HtmlAgilityPack;
using OpenQA.Selenium;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class PornhubParser : HtmlParser
{
    private const int MaxEntriesPerBatch = 25;
    
    public PornhubParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for pornhub.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var cookie = Config.Cookies.Pornhub;
        var cookieJar = Driver.GetCookieJar();
        cookieJar.AddCookie(new Cookie("il", cookie));
        cookieJar.AddCookie(new Cookie("accessAgeDisclaimerPH", "1"));
        cookieJar.AddCookie(new Cookie("adBlockAlertHidden", "1"));
        Driver.Refresh();
        var soup = await Soupify();
        string dirName;
        List<StringImageLinkWrapper> images;
        if (CurrentUrl.Contains("/model/") || CurrentUrl.Contains("/pornstar/"))
        {
            dirName = soup.SelectSingleNodeOrThrow("//h1[@itemprop='name']").InnerText;
            List<string> posts = [];
            var baseUrl = CurrentUrl.Split("/")[..5].Join("/");
            soup = await Soupify($"{baseUrl}/photos/public");
            var postNodes = soup.SelectNodes("//ul[@id='moreData']//a");
            if(postNodes is not null)
            {
                posts.AddRange(postNodes.Select(postNode => $"https://www.pornhub.com{postNode.GetHref()}"));
            }
    
            soup = await Soupify($"{baseUrl}/gifs/video");
            postNodes = soup.SelectNodes("//ul[@id='moreData']//a");
            if (postNodes is not null)
            {
                posts.AddRange(postNodes.Select(postNode => $"https://www.pornhub.com{postNode.GetHref()}"));
            }
    
            soup = await Soupify($"{baseUrl}/videos");
            postNodes = soup.SelectNodes("//ul[@id='uploadedVideosSection']//a");
            if (postNodes is not null)
            {
                posts.AddRange(postNodes.Select(postNode => $"https://www.pornhub.com{postNode.GetHref()}"));
            }
            
            while(true)
            {
                postNodes = soup.SelectNodes("//ul[@id='mostRecentVideosSection']//a");
                if (postNodes is null)
                {
                    break;
                }
                
                posts.AddRange(postNodes.Select(postNode => $"https://www.pornhub.com{postNode.GetHref()}"));
                var nextPage = soup.SelectSingleNode("//li[@class='page_next omega']");
                if (nextPage is null)
                {
                    break;
                }
                
                var nextPageUrl = nextPage.SelectSingleNodeOrThrow("./a").GetHref();
                soup = await Soupify($"https://www.pornhub.com{nextPageUrl}");
            }
            
            images = [];
            posts = posts.Where(post => !post.Contains("/channels/")
                                        && !post.Contains("/pornstar/")
                                        && !post.Contains("/model/")).ToList();
            foreach (var (i, post) in posts.Enumerate())
            {
                Log.Information("Parsing post {i}/{totalPosts}: {post}", i + 1, posts.Count, post);
                soup = await Soupify(post);
                var (postImages, _) = await PornhubLinkExtractor(soup);
                images.AddRange(postImages);
                if (i % 50 == 0)
                {
                    await Sleep(5000);
                }
            }
        }
        else
        {
            (images, dirName) = await PornhubLinkExtractor(soup);
        }
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }

    private async Task<(List<StringImageLinkWrapper> images, string dirName)> PornhubLinkExtractor(HtmlNode soup)
    {
        string dirName;
        List<StringImageLinkWrapper> images;
        if (CurrentUrl.Contains("view_video"))
        {
            Log.Debug("Parsing video page");
            dirName = soup.SelectSingleNodeOrThrow("//h1[@class='title']").SelectSingleNodeOrThrow(".//span").InnerText;
            var player = soup.SelectSingleNodeOrThrow("//div[@id='player']").SelectSingleNodeOrThrow(".//script");
            var js = player.InnerText;
            js = js.Split("var ")[1];
            var start = js.IndexOf('{');
            var rawJson = ExtractJsonObject(js[start..]);
            var jsonData = JsonSerializer.Deserialize<JsonNode>(rawJson);
            var mediaDefinitions = jsonData!["mediaDefinitions"]!.AsArray();
            var highestQuality = 0;
            var highestQualityUrl = "";
            foreach (var definition in mediaDefinitions)
            {
                var qualityJson = definition!["quality"]!;
                if (qualityJson.IsArray())
                {
                    continue;
                }
                
                var quality = qualityJson.Deserialize<string>()!.ToInt();
                if (quality > highestQuality)
                {
                    highestQuality = quality;
                    highestQualityUrl = definition["videoUrl"]!.Deserialize<string>()!;
                }
            }

            images = [highestQualityUrl];
        }
        else if (CurrentUrl.Contains("/album/"))
        {
            Log.Debug("Parsing album page");
            await LazyLoad(scrollBy: true);
            soup = await Soupify();
            dirName = soup.SelectSingleNodeOrThrow("//h1[@class='photoAlbumTitleV2']").InnerText.Trim();
            var posts = soup.SelectSingleNodeOrThrow("//ul[@class='photosAlbumsListing albumViews preloadImage']")
                            .SelectNodesOrThrow(".//a")
                            .Select(a => "https://www.pornhub.com" + a.GetHref());
            images = [];
            foreach (var post in posts)
            {
                CurrentUrl = post;
                await WaitForElement("//div[@id='photoImageSection']//img|//video[@class='centerImageVid']");
                soup = await Soupify();
                var imageNode = soup.SelectSingleNode("//div[@id='photoImageSection']//img");
                var url = imageNode is not null 
                    ? imageNode.GetSrc() 
                    : soup.SelectSingleNodeOrThrow("//video[@class='centerImageVid']/source").GetSrc();
                images.Add(url);
            }
        }
        else if(CurrentUrl.Contains("/gif/"))
        {
            Log.Debug("Parsing gif page");
            dirName = soup.SelectSingleNode("//div[@class='gifTitle']/h1")?.InnerText ?? "";
            if (dirName == "")
            {
                var id = CurrentUrl.Split("/")[4];
                dirName = $"Pornhub Gif {id}";
            }
            await WaitForElement("//video[@id='gifWebmPlayer']/source", timeout: 60);
            soup = await Soupify();
            var url = soup.SelectSingleNodeOrThrow("//video[@id='gifWebmPlayer']/source").GetSrc();
            images = [url];
        }
        else
        {
            throw new RipperException($"Unknown url: {CurrentUrl}");
        }

        return (images, dirName);
    }
}
