using System.Text.Json;
using System.Text.Json.Nodes;
using Core.DataStructures;
using Core.Enums;
using Core.Exceptions;
using Core.ExtensionMethods;
using HtmlAgilityPack;
using OpenQA.Selenium;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class PornhubParser : HtmlParser
{
    public PornhubParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for pornhub.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var cookie = Config.Cookies["Pornhub"];
        var cookieJar = Driver.Manage().Cookies;
        cookieJar.AddCookie(new Cookie("il", cookie));
        cookieJar.AddCookie(new Cookie("accessAgeDisclaimerPH", "1"));
        cookieJar.AddCookie(new Cookie("adBlockAlertHidden", "1"));
        Driver.Refresh();
        var soup = await Soupify();
        string dirName;
        List<StringImageLinkWrapper> images;
        if (CurrentUrl.Contains("/model/") || CurrentUrl.Contains("/pornstar/"))
        {
            dirName = soup.SelectSingleNode("//h1[@itemprop='name']").InnerText;
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
                
                var nextPageUrl = nextPage.SelectSingleNode("./a").GetHref();
                soup = await Soupify($"https://www.pornhub.com{nextPageUrl}");
            }
            
            images = [];
            posts = posts.Where(post => !post.Contains("/channels/")
                                        && !post.Contains("/pornstar/")
                                        && !post.Contains("/model/")).ToList();
            foreach (var (i, post) in posts.Enumerate())
            {
                Log.Information("Parsing post {i}/{totalPosts}", i + 1, posts.Count);
                soup = await Soupify(post);
                var (postImages, _) = await PornhubLinkExtractor(soup);
                images.AddRange(postImages);
                if (i % 50 == 0)
                {
                    await Task.Delay(5000);
                }
            }
        }
        else
        {
            (images, dirName) = await PornhubLinkExtractor(soup);
        }
        return new RipInfo(images, dirName, FilenameScheme);
    }

    private async Task<(List<StringImageLinkWrapper> images, string dirName)> PornhubLinkExtractor(HtmlNode soup)
    {
        string dirName;
        List<StringImageLinkWrapper> images;
        if (CurrentUrl.Contains("view_video"))
        {
            dirName = soup.SelectSingleNode("//h1[@class='title']").SelectSingleNode(".//span").InnerText;
            var player = soup.SelectSingleNode("//div[@id='player']").SelectSingleNode(".//script");
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
            await LazyLoad(scrollBy: true);
            soup = await Soupify();
            dirName = soup.SelectSingleNode("//h1[@class='photoAlbumTitleV2']").InnerText.Trim();
            var posts = soup.SelectSingleNode("//ul[@class='photosAlbumsListing albumViews preloadImage']")
                            .SelectNodes(".//a")
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
                    : soup.SelectSingleNode("//video[@class='centerImageVid']/source").GetSrc();
                images.Add(url);
            }
        }
        else if(CurrentUrl.Contains("/gif/"))
        {
            dirName = soup.SelectSingleNode("//div[@class='gifTitle']/h1")?.InnerText ?? "";
            if (dirName == "")
            {
                var id = CurrentUrl.Split("/")[4];
                dirName = $"Pornhub Gif {id}";
            }
            await WaitForElement("//video[@id='gifWebmPlayer']/source", timeout: -1);
            soup = await Soupify();
            var url = soup.SelectSingleNode("//video[@id='gifWebmPlayer']/source").GetSrc();
            images = [url];
        }
        else
        {
            throw new RipperException($"Unknown url: {CurrentUrl}");
        }

        return (images, dirName);
    }
}
