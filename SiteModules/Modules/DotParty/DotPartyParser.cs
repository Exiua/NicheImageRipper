using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using NicheImageRipper.Sdk.Common.ExtensionMethods;
using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.Exceptions;
using NicheImageRipper.Sdk.SiteParsing;
using NicheImageRipper.Sdk.Utility;
using NicheImageRipper.SiteModules.Modules.DotParty.Models;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.DotParty;

public abstract class DotPartyParser : ParameterizedHtmlParser
{
    private const int PageSize = 50;

    private static readonly string[] AttachmentExtensions =
        [".zip", ".rar", ".mp4", ".webm", ".psd", ".clip", ".m4v", ".7z", ".jpg", ".png", ".webp"];

    private static readonly string[] ParsableSites = ["drive.google.com", "mega.nz", /*"sendvid.com",*/ "dropbox.com"];

    protected abstract string[] OwnHosts { get; }
    protected abstract string CachePath { get; }

    protected DotPartyParser(WebDriver driver,
                             Dictionary<string, string> requestHeaders,
                             FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver,
        requestHeaders, filenameScheme)
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };
        HttpClient = new HttpClient(handler);
        // Needed due to DDG issues according to kemono themselves
        HttpClient.DefaultRequestHeaders.Add("Accept", "text/css");
    }

    /// <summary>
    ///     Parses the HTML for kemono.cr and coomer.cr and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <param name = "cancellationToken">A token to monitor for cancellation requests</param>
    /// <returns></returns>
    protected async Task<RipInfo> DotPartyParse(CancellationToken cancellationToken = default)
    {
        var domainUrl =
            new Uri(CurrentUrl).GetLeftPart(UriPartial.Authority); // e.g. "https://kemono.cr", "https://coomer.st"

        var cached = TryLoadCachedPosts();
        var (dirName, posts) = cached ?? await GetAndCachePosts(domainUrl, cancellationToken);

        var (files, externalLinks) = await ParseAllPosts(domainUrl, posts, cancellationToken);

        var dedupExternalLinks = DeduplicateExternalLinks(externalLinks);
        ExternalLinkExtractor.SaveExternalLinks(dedupExternalLinks);
        var resolvedLinks = await ResolveEmbeddedLinks(files, cancellationToken);
        var unique = DeduplicateFileLinks(resolvedLinks);

        File.Delete(CachePath);
        return RipInfo.FromUrlList(unique, dirName, FilenameScheme);
    }

    private bool IsNativeLink(string link)
    {
        try
        {
            var linkHost = new Uri(link).Host;
            return OwnHosts.Contains(linkHost, StringComparer.OrdinalIgnoreCase);
        }
        catch (UriFormatException)
        {
            return false;
        }
    }

    private async Task<List<StringFileLinkWrapper>> ResolveEmbeddedLinks(List<StringFileLinkWrapper> files,
                                                                         CancellationToken cancellationToken = default)
    {
        var stringLinks = new List<StringFileLinkWrapper>();
        foreach (var link in files)
        {
            if (IsNativeLink(link))
            {
                stringLinks.Add(link);
                continue;
            }

            try
            {
                var parser = CreateParser(link);
                var ripInfo = await parser.Parse(link, cancellationToken);
                stringLinks.AddRange(ripInfo);
            }
            catch (ParameterizedParserNotFound)
            {
                stringLinks.Add(link);
            }
        }

        return stringLinks;
    }

    private static Dictionary<string, List<string>> DeduplicateExternalLinks(
        Dictionary<string, List<string>> externalLinks)
    {
        foreach (var site in ExternalSites)
        {
            externalLinks[site] = externalLinks[site].RemoveDuplicates();
        }

        return externalLinks;
    }

    private static List<StringFileLinkWrapper> DeduplicateFileLinks(List<StringFileLinkWrapper> stringLinks)
    {
        // This may be able to be removed as RipInfo.FromUrlList also removes duplicates
        var seen = new HashSet<string>();
        var unique = new List<StringFileLinkWrapper>();
        foreach (var link in stringLinks)
        {
            var domainName = new Uri(link).Host.Split('.')[0];
            var linkStr = link.ToString();
            if (linkStr.Contains("puu.sh"))
            {
                // Site not responding correctly
                continue;
            }

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

        return unique;
    }

    private async Task<(List<StringFileLinkWrapper> Files, Dictionary<string, List<string>> ExternalLinks)>
        ParseAllPosts(
            string domainUrl, List<DotPartyPostResponse> posts, CancellationToken cancellationToken = default)
    {
        var files = new List<StringFileLinkWrapper>();
        var externalLinks = ExternalLinkExtractor.CreateExternalLinkDict();
        var numPosts = posts.Count;

        foreach (var (i, postResponse) in posts.Enumerate())
        {
            Logger.Information("Parsing post {PostNum} of {TotalPosts}", i + 1, numPosts);
            await ParsePost(postResponse, domainUrl, files, externalLinks, cancellationToken);
        }

        return (files, externalLinks);
    }

    private async Task ParsePost(DotPartyPostResponse response, string domainUrl, List<StringFileLinkWrapper> files,
                                 Dictionary<string, List<string>> externalLinks,
                                 CancellationToken cancellationToken = default)
    {
        var post = response.Post;
        var serverMap = BuildServerMap(response);

        Logger.Debug("Post ID: {PostId}", post.Id);
        var soup = await Soupify(post.Content, urlString: false, cancellationToken: cancellationToken);
        DotPartyLinkFixer.FixLinks(soup);

        var links = soup.SelectNodesSafe("//a").GetNullableHrefs().OfType<string>().ToList();
        var possibleLinks = ExtractPossibleLinkText(soup);

        MergeExternalLinks(externalLinks, ExternalLinkExtractor.ExtractExternalUrls(links));
        MergeExternalLinks(externalLinks, DotPartyExternalLinkExtractor.ExtractPossibleExternalUrls(possibleLinks));

        AddMainFile(post.File, domainUrl, serverMap, files);
        await AddAttachments(post.Attachments, domainUrl, serverMap, files, cancellationToken);

        var extractedAttachments = links.Where(l => AttachmentExtensions.Any(l.Contains))
                                        .Select(l => l.Contains(domainUrl) || l.Contains("http") ? l : domainUrl + l)
                                        .ToList();
        files.AddRange(extractedAttachments.ToStringFileLinks());

        foreach (var site in ParsableSites)
        {
            files.AddRange(externalLinks[site].ToStringFileLinkWrapperList());
        }
    }

    private static List<string> ExtractPossibleLinkText(HtmlAgilityPack.HtmlNode soup)
    {
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

        return possibleLinks;
    }

    private static void MergeExternalLinks(Dictionary<string, List<string>> into, Dictionary<string, List<string>> from)
    {
        foreach (var site in from.Keys)
        {
            into[site].AddRange(from[site]);
        }
    }

    private void AddMainFile(DotPartyFile file, string domainUrl,
                             IReadOnlyDictionary<string, string> serverMap, List<StringFileLinkWrapper> files)
    {
        if (file.Path is null)
        {
            return;
        }

        var url = ResolveUrl(file.Path, domainUrl, serverMap);
        // if path is not null, name should also not be null
        files.Add(FileLink.WithFilename(url, file.Name!, FilenameScheme));
    }

    private async Task AddAttachments(List<DotPartyAttachmentShort> attachments, string domainUrl,
                                      IReadOnlyDictionary<string, string> serverMap,
                                      List<StringFileLinkWrapper> files, CancellationToken cancellationToken = default)
    {
        foreach (var attachment in attachments)
        {
            var attachmentUrl = ResolveUrl(attachment.Path, domainUrl, serverMap);
            var specialCaseLinks = await CheckForSpecialCase(attachment.Name, attachmentUrl, cancellationToken);

            if (specialCaseLinks is null)
            {
                var filename = attachment.Name is not null
                    ? DotPartyExternalLinkExtractor.ReplacePlusWithSpaceInFilename(attachment.Name)
                    : "";
                files.Add(FileLink.WithFilename(attachmentUrl, filename, FilenameScheme));
            }
            else
            {
                files.AddRange(specialCaseLinks.ToStringFileLinks());
            }
        }
    }

    private async Task<List<string>?> CheckForSpecialCase(string? attachmentName, string attachmentUrl,
                                                          CancellationToken cancellationToken = default)
    {
        if (attachmentName is null ||
            !attachmentName.Contains("download", StringComparison.InvariantCultureIgnoreCase) ||
            !attachmentName.EndsWith(".txt")) // fanbox/user/4565149/
        {
            return null;
        }

        Logger.Debug("Fetching: {AttachmentUrl}", attachmentUrl);
        var response = await HttpClient.GetAsync(attachmentUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            Logger.Warning("Failed to retrieve special case attachment at {AttachmentUrl}", attachmentUrl);
            return null;
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return content.Split('\n')
                      .Where(line => line.StartsWith("https://mega.nz/"))
                      .Select(line => line.Trim(' ', '\r', '\n', '\t'))
                      .ToList();
    }

    private static string NormalizePath(string path, string domainUrl)
    {
        if (path[0] == '/')
        {
            path = domainUrl + path;
        }

        if (domainUrl.Contains("kemono"))
        {
            path = path.Replace("https://kemono.cr", "https://img.kemono.cr/thumbnail/data");
        }

        return path;
    }

    private static Dictionary<string, string> BuildServerMap(DotPartyPostResponse r) =>
        (r.Attachments ?? []).Select(a => (a.Path, a.Server))
                             .Concat((r.Videos ?? []).Select(v => (v.Path, v.Server))!)
                             .Concat((r.Previews ?? []).Select(p =>
                                  (p.Path, p.Server))) // last: previews may be thumbnail servers
                             .Where(t => t.Path is not null && t.Server is not null)
                             .GroupBy(t => t.Path!)
                             .ToDictionary(g => g.Key, g => g.First().Server!);

    private string ResolveUrl(string path, string domainUrl, IReadOnlyDictionary<string, string> serverMap) =>
        path.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? path
            : BuildFileUrl(path, serverMap.GetValueOrDefault(path), domainUrl);

    private (string DirName, List<DotPartyPostResponse> Posts)? TryLoadCachedPosts()
    {
        if (!File.Exists(CachePath))
        {
            return null;
        }

        var cache = JsonUtility.Deserialize<Dictionary<string, DotPartyCache>>(CachePath);
        if (cache is null || !cache.TryGetValue(CurrentUrl, out var siteCache))
        {
            return null;
        }

        Logger.Information("Using cached data for {Url}", CurrentUrl);
        return (siteCache.DirName, siteCache.Posts);
    }

    private async Task<(string, List<DotPartyPostResponse>)> GetAndCachePosts(
        string domainUrl, CancellationToken cancellationToken = default)
    {
        var (dirName, posts) = await GetPosts(domainUrl, cancellationToken);
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

    private async Task<(string, List<DotPartyPostResponse>)> GetPosts(string domainUrl,
                                                                      CancellationToken cancellationToken = default)
    {
        var baseUrl = CurrentUrl;
        var urlSplit = baseUrl.Split("/");
        var sourceSite = urlSplit[3];
        baseUrl = string.Join("/", urlSplit[3..6]).Split("?")[0];
        baseUrl = $"{domainUrl}/api/v1/{baseUrl}";
        Logger.Debug("Base URL: {BaseUrl}", baseUrl);
        var profileUrl = $"{baseUrl}/profile";
        Logger.Debug("Profile URL: {ProfileUrl}", profileUrl);
        var response = await RetryUntil(async () =>
            {
                var r = await HttpClient.GetAsync(profileUrl, cancellationToken);
                Logger.Debug("Profile page response: {StatusCode}", r.StatusCode);
                return r;
            }, (response) => response.IsSuccessStatusCode, "Failed to get profile page", delay: 5000,
            cancellationToken: cancellationToken);
        var json = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: cancellationToken);
        var dirName = json!.AsObject()["name"]!.Deserialize<string>()!;
        dirName = $"{dirName} - ({sourceSite})";
        Logger.Information("Parsed profile page: {DirName}", dirName);
        var posts = new List<DotPartyPostResponse>();
        var page = 0;
        while (true)
        {
            response = await RetryUntil(async () =>
                {
                    var r = await HttpClient.GetAsync($"{baseUrl}/posts?o={page * PageSize}", cancellationToken);
                    Logger.Debug("Page response: {StatusCode}", r.StatusCode);
                    return r;
                }, (r) => r.IsSuccessStatusCode, $"Failed to get page {page + 1}", delay: 5000,
                cancellationToken: cancellationToken);
            page++;
            Logger.Debug("Retrieving page {PageNum} of size {PageSize}", page, PageSize);
            var jsonPosts =
                await response.Content.ReadFromJsonAsync<List<DotPartyPostShort>>(cancellationToken: cancellationToken);
            if (jsonPosts is null)
            {
                throw new RipperException($"Failed to get posts on page {page}");
            }

            var ids = jsonPosts.Select(post => post.Id);
            foreach (var (i, id)in ids.Enumerate())
            {
                Logger.Debug("Retrieving post {PostId}", id);
                response = await RetryUntil(async () =>
                    {
                        var r = await HttpClient.GetAsync($"{baseUrl}/post/{id}", cancellationToken);
                        Logger.Debug("Post response: {StatusCode}", r.StatusCode);
                        return r;
                    }, (r) => r.IsSuccessStatusCode, $"Failed to get post {id}", delay: 15000,
                    cancellationToken: cancellationToken);

                var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var postJson = DeserializePost(rawJson);
                posts.Add(postJson);

                if ((i + 1) % 50 == 0)
                {
                    await Sleep(1000, cancellationToken);
                }
            }

            if (jsonPosts.Count < PageSize)
            {
                Logger.Debug("Reached end of posts");
                break;
            }

            await Sleep(250, cancellationToken);
        }

        return (dirName, posts);
    }

    protected abstract DotPartyPostResponse DeserializePost(string rawJson);
    protected abstract string BuildFileUrl(string path, string? server, string domainUrl);

    private async Task<List<string>?> CheckForSpecialCase(string domainUrl, string? attachmentName,
                                                          string attachmentPath,
                                                          CancellationToken cancellationToken = default)
    {
        List<string>? links = null;
        // ReSharper disable once InvertIf
        if (attachmentName is not null &&
            attachmentName.Contains("download", StringComparison.InvariantCultureIgnoreCase) &&
            attachmentName.EndsWith(".txt")) // fanbox/user/4565149/
        {
            links ??= [];
            var attachmentUrl = attachmentPath.StartsWith("https://") ? attachmentPath : domainUrl + attachmentPath;
            Logger.Debug("Fetching: {AttachmentUrl}", attachmentUrl);
            var response = await HttpClient.GetAsync(attachmentUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                Logger.Warning("Failed to retrieve special case attachment at {AttachmentUrl}", attachmentUrl);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var lines = content.Split('\n');
            links.AddRange(lines.Where(line => line.StartsWith("https://mega.nz/"))
                                .Select(line => line.Trim(' ', '\r', '\n', '\t')));
        }

        return links;
    }
}