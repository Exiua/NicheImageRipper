using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.Exceptions;
using Core.ExtensionMethods;
using Core.Managers;
using Core.Utility;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public abstract class DotPartyParser : ParameterizedHtmlParser
{
    private static readonly string[] ParsableSites = ["drive.google.com", "mega.nz", "sendvid.com", "dropbox.com"];
    
    protected DotPartyParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }
    
    /// <summary>
    ///     Parses the html for kemono.cr and coomer.cr and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <param name="domainUrl">The domain url of the site</param>
    /// <returns></returns>
    protected async Task<RipInfo> DotPartyParse(string domainUrl)
    {
        const int pageSize = 50;
        var baseUrl = CurrentUrl;
        var urlSplit = baseUrl.Split("/");
        var sourceSite = urlSplit[3];
        baseUrl = string.Join("/", urlSplit[3..6]).Split("?")[0];
        baseUrl = $"{domainUrl}/api/v1/{baseUrl}";
        Log.Debug("Base URL: {BaseUrl}", baseUrl);

        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };
        using var client = new HttpClient(handler);
        client.DefaultRequestHeaders.Add("Accept", "text/css"); // Needed due to DDG issues according to kemono themselves
        var profileUrl = $"{baseUrl}/profile";
        Log.Debug("Profile URL: {ProfileUrl}", profileUrl);
        var response = await RetryUntil(async () =>
            {
                var r = await client.GetAsync(profileUrl);
                Log.Debug("Profile page response: {StatusCode}", r.StatusCode);
                return r;
            }, 
            (response) => response.IsSuccessStatusCode,
            "Failed to get profile page", 
            delay: 5000);
        // for (var i = 0; i < 4; i++)
        // {
        //     response = await client.GetAsync(profileUrl);
        //     if (!response.IsSuccessStatusCode)
        //     {
        //         if (i == 3)
        //         {
        //             throw new RipperException("Failed to get profile page");
        //         }
        //
        //         await Sleep(5000);
        //         continue;
        //     }
        //     
        //     break;
        // }

        // var responseString = await response.Content.ReadAsByteArrayAsync();
        // await File.WriteAllBytesAsync("response.bin", responseString);
        var json = await response.Content.ReadFromJsonAsync<JsonNode>();
        var dirName = json!.AsObject()["name"]!.Deserialize<string>()!;
        dirName = $"{dirName} - ({sourceSite})";
        Log.Information("Parsed profile page: {DirName}", dirName);

        #region Get All Posts

        var posts = new List<JsonObject>();
        var page = 0;
        while (true)
        {
            response = await RetryUntil(
                async () =>
                {
                    var r = await client.GetAsync($"{baseUrl}/posts?o={page * pageSize}");
                    Log.Debug("Page response: {StatusCode}", r.StatusCode);
                    return r;
                },
                (r) => r.IsSuccessStatusCode,
                $"Failed to get page {page + 1}",
                delay: 5000);
            page++;
            Log.Debug("Retrieving page {PageNum} of size {PageSize}", page, pageSize);

            json = await response.Content.ReadFromJsonAsync<JsonNode>();
            var jsonPosts = json!.AsArray();
            var ids = jsonPosts.Select(post => post!.AsObject()["id"].Deserialize<string>()!);
            // Need to pull each post individually to get the html content body of the post
            foreach (var (i, id) in ids.Enumerate())
            {
                Log.Debug("Retrieving post {PostId}", id);
                response = await RetryUntil(
                    async () =>
                    {
                        var r = await client.GetAsync($"{baseUrl}/post/{id}");
                        Log.Debug("Post response: {StatusCode}", r.StatusCode);
                        return r;
                    },
                    (r) => r.IsSuccessStatusCode, 
                    $"Failed to get post {id}", 
                    delay: 15000);
                
                var postJson = await response.Content.ReadFromJsonAsync<JsonNode>();
                posts.Add(postJson!.AsObject());
                if ((i + 1) % 50 == 0)
                {
                    await Sleep(1000);
                }
            }
            //posts.AddRange(jsonPosts.Select(post => post!.AsObject()));
            if (jsonPosts.Count < pageSize)
            {
                Log.Debug("Reached end of posts");
                break;
            }

            await Sleep(250);
        }

        #endregion

        #region Parse All Posts

        var images = new List<StringImageLinkWrapper>();
        var externalLinks = CreateExternalLinkDict();
        var numPosts = posts.Count;
        string[] attachmentExtensions =
            [".zip", ".rar", ".mp4", ".webm", ".psd", ".clip", ".m4v", ".7z", ".jpg", ".png", ".webp"];

        foreach (var (i, postObject) in posts.Enumerate())
        {
            Log.Information("Parsing post {PostNum} of {TotalPosts}", i + 1, numPosts);
            var post = postObject["post"]!.AsObject();
            var id = post["id"]!.Deserialize<string>()!;
            Log.Debug("Post ID: {PostId}", id);
            var content = post["content"]!.Deserialize<string>()!;
            var soup = await Soupify(content, urlString: false);
            var links = soup.SelectNodesSafe("//a").GetHrefs();
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


            var file = post["file"]!.AsObject();
            var name = file["name"]?.Deserialize<string>();
            var path = file["path"]?.Deserialize<string>();
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

            var attachments = post["attachments"]!.AsArray();
            foreach (var attachment in attachments)
            {
                var attachmentName = attachment!["name"]!.Deserialize<string>()!;
                var attachmentPath = attachment["path"]!.Deserialize<string>()!;
                if (attachmentPath[0] == '/')
                {
                    attachmentPath = domainUrl + attachmentPath;
                }

                var attachmentLink = new ImageLink(attachmentPath, FilenameScheme, 0, filename: attachmentName);
                images.Add(attachmentLink);
            }

            var extractedAttachments = links
                                      .Where(l => attachmentExtensions.Any(l.Contains))
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

        return RipInfo.FromUrlList(unique, dirName, FilenameScheme);
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
}