using System.Text.Json;
using System.Text.Json.Nodes;
using HtmlAgilityPack;
using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Exceptions;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.Utility;
using OpenQA.Selenium;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers;

public class PornhubParser : TimeSensitiveHtmlParser, IHtmlParser
{
    public static string ParserName => "pornhub";

    protected override string ImageLinksFileName => "pornhub.json";
    protected override int MaxEntriesPerBatch => 25;
    protected override string ParserKey => "pornhub";


    public PornhubParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders,
                         FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager,
        requestHeaders, IHtmlParser.GetFilenameScheme<PornhubParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the HTML for pornhub.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var cookie = Config.Cookies.Pornhub;
        var cookieJar = Driver.GetCookieJar();
        cookieJar.AddCookie(new Cookie("il", cookie));
        cookieJar.AddCookie(new Cookie("accessAgeDisclaimerPH", "1"));
        cookieJar.AddCookie(new Cookie("adBlockAlertHidden", "1"));
        Driver.Refresh();
        StoreLastLink(CurrentUrl);
        var soup = await Soupify(cancellationToken: cancellationToken);
        string dirName;
        List<StringFileLinkWrapper> images;
        Dictionary<string, List<string>> cachedLinks;
        if (CurrentUrl.Contains("/model/") || CurrentUrl.Contains("/pornstar/"))
        {
            dirName = soup.SelectSingleNodeOrThrow("//h1[@itemprop='name']").InnerText;

            List<string> posts;
            if (File.Exists(ImageLinksFileName))
            {
                var deserializedCachedLinks =
                    JsonUtility.Deserialize<Dictionary<string, List<string>>>(ImageLinksFileName);
                if (deserializedCachedLinks is null)
                {
                    posts = await GetLinks(cancellationToken);
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
                        posts = await GetLinks(cancellationToken);
                        cachedLinks[CurrentUrl] = posts;
                        JsonUtility.Serialize(ImageLinksFileName, cachedLinks);
                    }
                }
            }
            else
            {
                posts = await GetLinks(cancellationToken);
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
                        Logger.Information("Parsing post {i}/{totalPosts}: {post}", i + 1, posts.Count, post);
                        soup = await Soupify(post, cancellationToken: cancellationToken);
                        var (postImages, _, extraPosts) = await PornhubLinkExtractor(soup, cancellationToken);
                        if (extraPosts is not null)
                        {
                            cachePosts.AddRange(extraPosts);
                        }
                        else
                        {
                            cachePosts.Add(post);
                        }

                        images.AddRange(postImages);
                        if (i % 50 == 0)
                        {
                            await Sleep(5000, cancellationToken);
                        }

                        break;
                    }
                    catch (ElementNotFoundException)
                    {
                        Logger.Warning("Element not found while parsing post {post}", post);
                        await Sleep(250, cancellationToken);
                    }
                    catch (WebDriverException e)
                    {
                        if (e.Message.EndsWith("timed out after 60 seconds."))
                        {
                            Logger.Warning("Timeout while parsing post {post}", post);
                            WebDriver.RegenerateDriver();
                            await Sleep(250, cancellationToken);
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
            var (tempImages, dir, extraPosts) = await PornhubLinkExtractor(soup, cancellationToken);
            images = tempImages;
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
    protected override async Task<string> UpdateLink(string link, CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(link, cancellationToken: cancellationToken);
        var (images, _, _) = await PornhubLinkExtractor(soup, cancellationToken);
        return images[0];
    }

    private async Task<List<string>> GetLinks(CancellationToken cancellationToken = default)
    {
        List<string> posts = [];
        var baseUrl = CurrentUrl.Split("/")[..5].Join("/");
        var soup = await Soupify($"{baseUrl}/photos/public", cancellationToken: cancellationToken);
        var postNodes = soup.SelectNodes("//ul[@id='moreData']//a");
        if (postNodes is not null)
        {
            posts.AddRange(postNodes.Select(postNode => $"https://www.pornhub.com{postNode.GetHref()}"));
        }

        soup = await Soupify($"{baseUrl}/gifs/video", cancellationToken: cancellationToken);
        postNodes = soup.SelectNodes("//ul[@id='moreData']//a");
        if (postNodes is not null)
        {
            posts.AddRange(postNodes.Select(postNode => $"https://www.pornhub.com{postNode.GetHref()}"));
        }

        soup = await Soupify($"{baseUrl}/videos", cancellationToken: cancellationToken);
        postNodes = soup.SelectNodes("//ul[@id='uploadedVideosSection']//a");
        if (postNodes is not null)
        {
            posts.AddRange(postNodes.Select(postNode => $"https://www.pornhub.com{postNode.GetHref()}"));
        }

        while (true)
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
            soup = await Soupify($"https://www.pornhub.com{nextPageUrl}", cancellationToken: cancellationToken);
        }

        posts = posts.Where(post => !post.Contains("/channels/")
                                    && !post.Contains("/pornstar/")
                                    && !post.Contains("/model/")).ToList();

        return posts;
    }

    private async Task<(List<StringFileLinkWrapper> images, string dirName, List<string>? extraPosts)>
        PornhubLinkExtractor(HtmlNode soup, CancellationToken cancellationToken = default)
    {
        // First parse may have expired links as we need to get number of links per post
        //  This mainly is due to albums as videos and gifs only have one link per post
        string dirName;
        List<StringFileLinkWrapper> files;
        List<string>? extraPosts = null;
        if (CurrentUrl.Contains("view_video"))
        {
            Logger.Debug("Parsing video page");
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

            var fileLink = FileLink.WithFilename(highestQualityUrl, $"{dirName}.mp4", FilenameScheme,
                cleanFilename: true, linkInfo: LinkInfo.ObfuscatedM3U8, referer: "https://pornhub.com/");
            files = [fileLink];
        }
        else if (CurrentUrl.Contains("/album/"))
        {
            Logger.Debug("Parsing album page");
            await LazyLoad(scrollBy: true, cancellationToken: cancellationToken);
            soup = await Soupify(cancellationToken: cancellationToken);
            dirName = soup.SelectSingleNodeOrThrow("//h1[@class='photoAlbumTitleV2']").InnerText.Trim();
            var posts = soup.SelectSingleNodeOrThrow("//ul[@class='photosAlbumsListing albumViews preloadImage']")
                            .SelectNodesOrThrow(".//a")
                            .Select(a => "https://www.pornhub.com" + a.GetHref())
                            .ToList();
            files = [];
            foreach (var post in posts)
            {
                var url = await ExtractPhoto(post, cancellationToken);
                files.Add(url);
            }

            extraPosts = posts;
        }
        else if (CurrentUrl.Contains("/photo/"))
        {
            dirName = CurrentUrl.Split("/")[4];
            var url = await ExtractPhoto(cancellationToken: cancellationToken);
            files = [url];
        }
        else if (CurrentUrl.Contains("/gif/"))
        {
            Logger.Debug("Parsing gif page");
            dirName = soup.SelectSingleNode("//div[@class='gifTitle']/h1")?.InnerText ?? "";
            if (dirName == "")
            {
                var id = CurrentUrl.Split("/")[4];
                dirName = $"Pornhub Gif {id}";
            }

            await WaitForElement("//video[@id='gifWebmPlayer']/source", timeout: 60,
                cancellationToken: cancellationToken);
            soup = await Soupify(cancellationToken: cancellationToken);
            var url = soup.SelectSingleNodeOrThrow("//video[@id='gifWebmPlayer']/source").GetSrc();
            files = [url];
        }
        else
        {
            throw new RipperException($"Unknown url: {CurrentUrl}");
        }

        return (files, dirName, extraPosts);
    }

    private async Task<string> ExtractPhoto(string post = "", CancellationToken cancellationToken = default)
    {
        HtmlNode soup;
        if (post != "")
        {
            soup = await Soupify(post, xpath: "//div[@id='photoImageSection']//img|//video[@class='centerImageVid']",
                cancellationToken: cancellationToken);
        }
        else
        {
            soup = await Soupify(xpath: "//div[@id='photoImageSection']//img|//video[@class='centerImageVid']",
                cancellationToken: cancellationToken);
        }

        var imageNode = soup.SelectSingleNode("//div[@id='photoImageSection']//img");
        var url = imageNode is not null
            ? imageNode.GetSrc()
            : soup.SelectSingleNodeOrThrow("//video[@class='centerImageVid']/source").GetSrc();

        return url;
    }
}