using Common.ExtensionMethods;
using System.Text.Json;
using System.Text.Json.Nodes;
using Core.DataStructures;
using Core.Enums;
using Core.Exceptions;
using Core.ExtensionMethods;
using Core.Managers;
using Core.Utility;
using HtmlAgilityPack;
using OpenQA.Selenium;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class PornhubParser : TimeSensitiveHtmlParser, IHtmlParser
{
    public static string ParserName => "pornhub";

    protected override string ImageLinksFileName => "pornhub.json";
    protected override int MaxEntriesPerBatch => 25;
    protected override string ParserKey => "pornhub";


    public PornhubParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<PornhubParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for pornhub.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse()
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
        Dictionary<string, List<string>> cachedLinks;
        if (CurrentUrl.Contains("/model/") || CurrentUrl.Contains("/pornstar/"))
        {
            dirName = soup.SelectSingleNodeOrThrow("//h1[@itemprop='name']").InnerText;

            List<string> posts;
            if (File.Exists(ImageLinksFileName))
            {
                var deserializedCachedLinks = JsonUtility.Deserialize<Dictionary<string, List<string>>>(ImageLinksFileName);
                if (deserializedCachedLinks is null)
                {
                    posts = await GetLinks();
                    cachedLinks = new Dictionary<string, List<string>>
                    {
                        [CurrentUrl] = posts
                    };
                    
                    JsonUtility.Serialize(ImageLinksFileName, cachedLinks);
                }
                else
                {
                    cachedLinks = deserializedCachedLinks;
                    if (cachedLinks.TryGetValue(CurrentUrl, out var cachedPosts))
                    {
                        posts = cachedPosts;
                    }
                    else
                    {
                        posts = await GetLinks();
                        cachedLinks[CurrentUrl] = posts;
                        JsonUtility.Serialize(ImageLinksFileName, cachedLinks);
                    }
                }
            }
            else
            {
                posts = await GetLinks();
                cachedLinks = new Dictionary<string, List<string>>
                {
                    [CurrentUrl] = posts
                };

                JsonUtility.Serialize(ImageLinksFileName, cachedLinks);
            }

            images = [];
            var cachePosts = new List<string>();
            foreach (var (i, post) in posts.Enumerate())
            {
                while (true)
                {
                    try
                    {
                        Log.Information("Parsing post {i}/{totalPosts}: {post}", i + 1, posts.Count, post);
                        soup = await Soupify(post);
                        var (postImages, _, extraPosts) = await PornhubLinkExtractor(soup);
                        if (extraPosts is not null)
                        {
                            cachePosts.AddRange(extraPosts);
                        }
                        else
                        {
                            cachePosts.Add(post);
                        }

                        images.AddRange(postImages.ToStringImageLinks());
                        if (i % 50 == 0)
                        {
                            await Sleep(5000);
                        }
                        
                        break;
                    }
                    catch (WebDriverException e)
                    {
                        if (e.Message.EndsWith("timed out after 60 seconds."))
                        {
                            Log.Warning("Timeout while parsing post {post}", post);
                            WebDriver.RegenerateDriver();
                            await Sleep(250);
                        }
                    }
                }
            }
            
            // Allows of replacement of albums with each individual photo link
            if (cachePosts.Count > posts.Count)
            {
                cachedLinks[CurrentUrl] = cachePosts;
                JsonUtility.Serialize(ImageLinksFileName, cachedLinks);
            }
        }
        else
        {
            var (tempImages, dir, extraPosts) = await PornhubLinkExtractor(soup);
            images = tempImages.ToStringImageLinkWrapperList();
            dirName = dir;
            if (extraPosts is not null)
            {
                cachedLinks = new Dictionary<string, List<string>>
                {
                    [CurrentUrl] = extraPosts
                };

                JsonUtility.Serialize(ImageLinksFileName, cachedLinks);
            }
        }
        
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
    
    // albums should never be passed to this method
    protected override async Task<string> UpdateLink(string link)
    {
        var soup = await Soupify(link);
        var (images, _, _) = await PornhubLinkExtractor(soup);
        return images[0];
    }

    private async Task<List<string>> GetLinks()
    {
        List<string> posts = [];
        var baseUrl = CurrentUrl.Split("/")[..5].Join("/");
        var soup = await Soupify($"{baseUrl}/photos/public");
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
        
        posts = posts.Where(post => !post.Contains("/channels/")
                                    && !post.Contains("/pornstar/")
                                    && !post.Contains("/model/")).ToList();

        return posts;
    }

    private async Task<(List<string> images, string dirName, List<string>? extraPosts)> PornhubLinkExtractor(HtmlNode soup)
    {
        // First parse may have expired links as we need to get number of links per post
        //  This mainly is due to albums as videos and gifs only have one link per post
        string dirName;
        List<string> images;
        List<string>? extraPosts = null;
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
                
                var quality = qualityJson.Deserialize<string>()!.ParseInt();
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
                            .Select(a => "https://www.pornhub.com" + a.GetHref())
                            .ToList();
            images = [];
            foreach (var post in posts)
            {
                var url = await ExtractPhoto(post);
                images.Add(url);
            }
            
            extraPosts = posts;
        }
        else if (CurrentUrl.Contains("/photo/"))
        {
            dirName = CurrentUrl.Split("/")[4];
            var url = await ExtractPhoto();
            images = [url];
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

        return (images, dirName, extraPosts);
    }

    private async Task<string> ExtractPhoto(string post = "")
    {
        HtmlNode soup;
        if (post != "")
        {
            soup = await Soupify(post, xpath: "//div[@id='photoImageSection']//img|//video[@class='centerImageVid']");
        }
        else
        {
            soup = await Soupify(xpath: "//div[@id='photoImageSection']//img|//video[@class='centerImageVid']");
        }

        var imageNode = soup.SelectSingleNode("//div[@id='photoImageSection']//img");
        var url = imageNode is not null 
            ? imageNode.GetSrc() 
            : soup.SelectSingleNodeOrThrow("//video[@class='centerImageVid']/source").GetSrc();

        return url;
    }
}
