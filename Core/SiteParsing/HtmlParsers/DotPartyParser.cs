using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.Exceptions;
using Core.ExtensionMethods;
using Core.Managers;
using Core.Utility;
using HtmlAgilityPack;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public abstract class DotPartyParser : ParameterizedHtmlParser
{
    private const string CachePath = "dotpartyCache.json";
    private const int PageSize = 50;

    private static readonly string[] AttachmentExtensions =
        [".zip", ".rar", ".mp4", ".webm", ".psd", ".clip", ".m4v", ".7z", ".jpg", ".png", ".webp"];

    private static readonly string[] ParsableSites = ["drive.google.com", "mega.nz", "sendvid.com", "dropbox.com"];

    private readonly HttpClient _httpClient;

    protected DotPartyParser(WebDriver driver, ApiClientManager clientManager,
                             Dictionary<string, string> requestHeaders,
                             FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager,
        requestHeaders, filenameScheme)
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };

        _httpClient = new HttpClient(handler);
        _httpClient.DefaultRequestHeaders.Add("Accept",
            "text/css"); // Needed due to DDG issues according to kemono themselves
    }

    protected override void DisposeInternal()
    {
        _httpClient.Dispose();
    }

    /// <summary>
    ///     Parses the html for kemono.cr and coomer.cr and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <param name="domainUrl">The domain url of the site</param>
    /// <returns></returns>
    protected async Task<RipInfo> DotPartyParse(string domainUrl)
    {
        string dirName;
        List<DotPartyPostResponse> posts;
        if (File.Exists(CachePath))
        {
            var cache = JsonUtility.Deserialize<Dictionary<string, DotPartyCache>>(CachePath);
            if (cache is not null && cache.TryGetValue(CurrentUrl, out var siteCache))
            {
                dirName = siteCache.DirName;
                posts = siteCache.Posts;
                Log.Information("Using cached data for {Url}", CurrentUrl);
            }
            else
            {
                (dirName, posts) = await GetAndCachePosts(domainUrl);
            }
        }
        else
        {
            (dirName, posts) = await GetAndCachePosts(domainUrl);
        }

        #region Parse All Posts

        var images = new List<StringImageLinkWrapper>();
        var externalLinks = CreateExternalLinkDict();
        var numPosts = posts.Count;

        foreach (var (i, postResponse) in posts.Enumerate())
        {
            Log.Information("Parsing post {PostNum} of {TotalPosts}", i + 1, numPosts);
            var post = postResponse.Post;
            var id = post.Id;
            Log.Debug("Post ID: {PostId}", id);
            var content = post.Content;
            var soup = await Soupify(content, urlString: false);
            FixLinks(soup);
            var links = soup.SelectNodesSafe("//a").GetNullableHrefs().OfType<string>().ToList();
            var possibleLinks = new List<string>();
            var possibleLinksP = soup.SelectNodes("//p");
            if (possibleLinksP is not null)
            {
                possibleLinks.AddRange(possibleLinksP.Select(p => p.InnerText));
            }

            var possibleLinksDiv = soup.SelectNodes("//div");
            if (possibleLinksDiv is not null)
            {
                possibleLinks.AddRange(possibleLinksDiv.Select(d => d.InnerText));
            }

            var extLinks = ExtractExternalUrls(links);
            foreach (var site in extLinks.Keys)
            {
                externalLinks[site].AddRange(extLinks[site]);
            }

            extLinks = ExtractPossibleExternalUrls(possibleLinks);
            foreach (var site in extLinks.Keys)
            {
                externalLinks[site].AddRange(extLinks[site]);
            }


            var file = post.File;
            var name = file.Name;
            var path = file.Path;
            if (path is not null)
            {
                if (path[0] == '/')
                {
                    path = domainUrl + path;
                }

                // if path is not null, name should also not be null
                var imageLink = new ImageLink(path, FilenameScheme, 0, filename: name!);
                images.Add(imageLink);
            }

            var attachments = post.Attachments;
            foreach (var attachment in attachments)
            {
                var attachmentName = attachment.Name;
                var attachmentPath = attachment.Path;
                if (attachmentPath[0] == '/')
                {
                    attachmentPath = domainUrl + attachmentPath;
                }

                var specialCaseLinks = await CheckForSpecialCase(domainUrl, attachmentName, attachmentPath);
                if (specialCaseLinks is null)
                {
                    var filename = attachmentName is not null ? ReplacePlusWithSpaceInFilename(attachmentName) : "";
                    var attachmentLink = new ImageLink(attachmentPath, FilenameScheme, 0, filename: filename);
                    images.Add(attachmentLink);
                }
                else
                {
                    images.AddRange(specialCaseLinks.ToStringImageLinks());
                }
            }

            var extractedAttachments = links
                                      .Where(l => AttachmentExtensions.Any(l.Contains))
                                      .Select(l => (l.Contains(domainUrl) || l.Contains("http")) ? l : domainUrl + l)
                                      .ToList();


            images.AddRange(extractedAttachments.ToStringImageLinks());
            foreach (var site in ParsableSites)
            {
                images.AddRange(externalLinks[site].ToStringImageLinkWrapperList());
            }
        }

        #endregion

        foreach (var site in ExternalSites)
        {
            externalLinks[site] = externalLinks[site].RemoveDuplicates();
        }

        SaveExternalLinks(externalLinks);
        var stringLinks = new List<StringImageLinkWrapper>();
        foreach (var link in images)
        {
            if (!link.Contains("dropbox.com/"))
            {
                stringLinks.Add(link);
            }
            else
            {
                var dropboxParser = new DropboxParser(WebDriver, ApiClientManager, RequestHeaders, FilenameScheme);
                var ripInfo = await dropboxParser.Parse(link);
                stringLinks.AddRange(ripInfo.Urls.ToStringImageLinks());
            }
        }

        // This may be able to be removed as RipInfo.FromUrlList also removes duplicates
        // Remove duplicates
        var seen = new HashSet<string>();
        var unique = new List<StringImageLinkWrapper>();
        foreach (var link in stringLinks)
        {
            var domainName = new Uri(link).Host.Split('.')[0];
            var linkStr = link.ToString();
            if (!linkStr.Contains(domainName)) // Only native links get duplicated
            {
                unique.Add(link);
                continue;
            }

            var fileName = linkStr.Split("/")[^1];
            if (seen.Add(fileName))
            {
                unique.Add(link);
            }
        }

        File.Delete(CachePath);

        return RipInfo.FromUrlList(unique, dirName, FilenameScheme);
    }

    private static string ReplacePlusWithSpaceInFilename(string filename)
    {
        var newFilename = "";
        var lastChar = '\0';
        foreach (var c in filename)
        {
            switch (c)
            {
                case '+':
                    if (lastChar == '+')
                    {
                        newFilename += '+';
                    }
                    break;
                default:
                    if (lastChar == '+')
                    {
                        newFilename += ' ';
                    }
                    
                    newFilename += c;
                    break;
            }
            
            lastChar = c;
        }
        
        return newFilename;
    }
    
    private static Dictionary<string, List<string>> ExtractPossibleExternalUrls(List<string> possibleUrls)
    {
        var externalLinks = CreateExternalLinkDict();
        foreach (var site in externalLinks.Keys)
        {
            foreach (var text in possibleUrls)
            {
                if (!text.Contains(site))
                {
                    continue;
                }

                var parts = text.Split();
                foreach (var part in parts)
                {
                    if (!part.Contains(site))
                    {
                        continue;
                    }

                    var link = UrlUtility.ExtractUrl(part);
                    if (link != "")
                    {
                        externalLinks[site].Add(link + '\n');
                    }
                }
            }
        }

        return externalLinks;
    }

    private async Task<(string, List<DotPartyPostResponse>)> GetAndCachePosts(string domainUrl)
    {
        var (dirName, posts) = await GetPosts(domainUrl);
        var siteCache = new DotPartyCache
        {
            DirName = dirName,
            Posts = posts
        };

        var cache = new Dictionary<string, DotPartyCache>
        {
            [CurrentUrl] = siteCache
        };

        JsonUtility.Serialize(CachePath, cache);
        return (dirName, posts);
    }

    private async Task<(string, List<DotPartyPostResponse>)> GetPosts(string domainUrl)
    {
        var baseUrl = CurrentUrl;
        var urlSplit = baseUrl.Split("/");
        var sourceSite = urlSplit[3];
        baseUrl = string.Join("/", urlSplit[3..6]).Split("?")[0];
        baseUrl = $"{domainUrl}/api/v1/{baseUrl}";
        Log.Debug("Base URL: {BaseUrl}", baseUrl);
        var profileUrl = $"{baseUrl}/profile";
        Log.Debug("Profile URL: {ProfileUrl}", profileUrl);
        var response = await RetryUntil(async () =>
            {
                var r = await _httpClient.GetAsync(profileUrl);
                Log.Debug("Profile page response: {StatusCode}", r.StatusCode);
                return r;
            },
            (response) => response.IsSuccessStatusCode,
            "Failed to get profile page",
            delay: 5000);

        // var responseString = await response.Content.ReadAsByteArrayAsync();
        // await File.WriteAllBytesAsync("response.bin", responseString);
        var json = await response.Content.ReadFromJsonAsync<JsonNode>();
        var dirName = json!.AsObject()["name"]!.Deserialize<string>()!;
        dirName = $"{dirName} - ({sourceSite})";
        Log.Information("Parsed profile page: {DirName}", dirName);
        var posts = new List<DotPartyPostResponse>();
        var page = 0;
        while (true)
        {
            response = await RetryUntil(
                async () =>
                {
                    var r = await _httpClient.GetAsync($"{baseUrl}/posts?o={page * PageSize}");
                    Log.Debug("Page response: {StatusCode}", r.StatusCode);
                    return r;
                },
                (r) => r.IsSuccessStatusCode,
                $"Failed to get page {page + 1}",
                delay: 5000);
            page++;
            Log.Debug("Retrieving page {PageNum} of size {PageSize}", page, PageSize);

            var jsonPosts = await response.Content.ReadFromJsonAsync<List<DotPartyPostShort>>();
            if (jsonPosts is null)
            {
                throw new RipperException($"Failed to get posts on page {page}");
            }

            var ids = jsonPosts.Select(post => post.Id);
            // Need to pull each post individually to get the html content body of the post
            foreach (var (i, id) in ids.Enumerate())
            {
                Log.Debug("Retrieving post {PostId}", id);
                response = await RetryUntil(
                    async () =>
                    {
                        var r = await _httpClient.GetAsync($"{baseUrl}/post/{id}");
                        Log.Debug("Post response: {StatusCode}", r.StatusCode);
                        return r;
                    },
                    (r) => r.IsSuccessStatusCode,
                    $"Failed to get post {id}",
                    delay: 15000);

                var postJson = await response.Content.ReadFromJsonAsync<DotPartyPostResponse>();
                if (postJson is null)
                {
                    throw new RipperException($"Failed to get post {id}");
                }

                posts.Add(postJson);
                if ((i + 1) % 50 == 0)
                {
                    await Sleep(1000);
                }
            }

            if (jsonPosts.Count < PageSize)
            {
                Log.Debug("Reached end of posts");
                break;
            }

            await Sleep(250);
        }

        return (dirName, posts);
    }

    private async Task<List<string>?> CheckForSpecialCase(string domainUrl, string? attachmentName,
                                                          string attachmentPath)
    {
        List<string>? links = null;
        // ReSharper disable once InvertIf
        if (attachmentName is not null &&
            attachmentName.Contains("download", StringComparison.InvariantCultureIgnoreCase) &&
            attachmentName.EndsWith(".txt")) // fanbox/user/4565149/
        {
            links ??= [];
            var attachmentUrl = attachmentPath.StartsWith("https://") ? attachmentPath : domainUrl + attachmentPath;
            Log.Debug("Fetching: {AttachmentUrl}", attachmentUrl);
            var response = await _httpClient.GetAsync(attachmentUrl);
            if (!response.IsSuccessStatusCode)
            {
                Log.Warning("Failed to retrieve special case attachment at {AttachmentUrl}", attachmentUrl);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var lines = content.Split('\n');
            links.AddRange(lines.Where(line => line.StartsWith("https://mega.nz/"))
                                .Select(line => line.Trim(' ', '\r', '\n', '\t')));
        }

        return links;
    }

    // Fixes links that have been split
    // So far, only mega.nz links have been observed to be split along the hash (#) character if present in the link
    private static void FixLinks(HtmlNode soup)
    {
        // Select all anchor tags under this root
        var anchors = soup.SelectNodes(".//a");
        if (anchors is null)
        {
            return;
        }

        // Track text nodes to remove
        var nodesToRemove = new List<HtmlNode>();

        HtmlNode? anchorToFix = null;
        foreach (var anchor in anchors)
        {
            var parent = anchor.ParentNode;
            foreach (var node in parent.ChildNodes)
            {
                switch (node.NodeType)
                {
                    case HtmlNodeType.Element:
                    {
                        // Check only <a> nodes
                        if (node.Name.Equals("a", StringComparison.OrdinalIgnoreCase))
                        {
                            var href = node.GetAttributeValue("href", "");
                            if (href.Contains("https://mega.nz/"))
                            {
                                // Mark anchor as needing merge
                                anchorToFix = node;
                            }
                        }

                        break;
                    }
                    case HtmlNodeType.Text:
                    {
                        if (anchorToFix != null)
                        {
                            var text = node.InnerText;
                            // The hash extension must begin with "#"
                            if (!text.StartsWith('#'))
                            {
                                // Anchor was not split, skip
                                anchorToFix = null;
                                continue;
                            }

                            // Merge text into anchor
                            var newHref = anchorToFix.InnerText + text;
                            anchorToFix.InnerHtml = HtmlDocument.HtmlEncode(newHref); // display text
                            anchorToFix.SetAttributeValue("href", newHref); // link URL

                            // Schedule removal of trailing text node
                            nodesToRemove.Add(node);

                            anchorToFix = null;
                        }

                        break;
                    }
                    case HtmlNodeType.Document:
                    case HtmlNodeType.Comment:
                        anchorToFix = null; // This probably shouldn't happen, but just in case
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        // Now remove all text nodes that were merged
        foreach (var n in nodesToRemove)
        {
            n.Remove();
        }
    }
}

public class DotPartyCache
{
    public string DirName { get; set; } = null!;
    public List<DotPartyPostResponse> Posts { get; set; } = null!;
}

public class DotPartyPostShort
{
    [JsonPropertyName("id")] public string Id { get; set; } = null!;
    [JsonPropertyName("user")] public string User { get; set; } = null!;
    [JsonPropertyName("service")] public string Service { get; set; } = null!;
    [JsonPropertyName("title")] public string Title { get; set; } = null!;
    [JsonPropertyName("substring")] public string Substring { get; set; } = null!;
    [JsonPropertyName("published")] public string Published { get; set; } = null!;
    [JsonPropertyName("file")] public DotPartyFile File { get; set; } = null!;
    [JsonPropertyName("attachments")] public List<DotPartyAttachment> Attachments { get; set; } = null!;
}

public class DotPartyFile
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("path")] public string? Path { get; set; }
}

public class DotPartyAttachment
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("path")] public string Path { get; set; } = null!;
}

public class DotPartyPostResponse
{
    [JsonPropertyName("post")] public DotPartyPostFull Post { get; set; } = null!;
    [JsonPropertyName("attachments")] public List<DotPartyAttachment> Attachments { get; set; } = null!;
    [JsonPropertyName("previews")] public List<DotPartyPreview> Previews { get; set; } = null!;
    [JsonPropertyName("videos")] public List<DotPartyVideo> Videos { get; set; } = null!;
    [JsonPropertyName("props")] public DotPartyProps Props { get; set; } = null!;
}

public class DotPartyPostFull
{
    [JsonPropertyName("id")] public string Id { get; set; } = null!;
    [JsonPropertyName("user")] public string User { get; set; } = null!;
    [JsonPropertyName("service")] public string Service { get; set; } = null!;
    [JsonPropertyName("title")] public string Title { get; set; } = null!;
    [JsonPropertyName("content")] public string Content { get; set; } = null!;
    [JsonPropertyName("embed")] public JsonElement Embed { get; set; }
    [JsonPropertyName("shared_file")] public bool SharedFile { get; set; }
    [JsonPropertyName("added")] public string? Added { get; set; }
    [JsonPropertyName("published")] public string Published { get; set; } = null!;
    [JsonPropertyName("edited")] public string Edited { get; set; } = null!;
    [JsonPropertyName("file")] public DotPartyFile File { get; set; } = null!;
    [JsonPropertyName("attachments")] public List<DotPartyAttachment> Attachments { get; set; } = null!;
    [JsonPropertyName("poll")] public JsonElement? Poll { get; set; }
    [JsonPropertyName("captions")] public JsonElement? Captions { get; set; }
    [JsonPropertyName("tags")] public JsonArray Tags { get; set; } = null!;

    [JsonPropertyName("incomplete_rewards")]
    public JsonObject? IncompleteRewards { get; set; }

    [JsonPropertyName("next")] public string? Next { get; set; }
    [JsonPropertyName("prev")] public string? Prev { get; set; }
}

public class DotPartyPreview
{
    [JsonPropertyName("type")] public string Type { get; set; } = null!;
    [JsonPropertyName("server")] public string Server { get; set; } = null!;
    [JsonPropertyName("name")] public string Name { get; set; } = null!;
    [JsonPropertyName("path")] public string Path { get; set; } = null!;
}

public class DotPartyVideo
{
    [JsonExtensionData] private Dictionary<string, JsonElement> Fields { get; set; } = null!;
}

public class DotPartyProps
{
    [JsonPropertyName("flagged")] public string? Flagged { get; set; }

    [JsonPropertyName("revisions")]
    public List<List<JsonNode>> Revisions { get; set; } =
        null!; // Each sublist contains [revision number, DotPartyPostFull]
}