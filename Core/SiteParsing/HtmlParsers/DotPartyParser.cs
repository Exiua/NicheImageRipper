using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.Exceptions;
using Core.ExtensionMethods;
using Core.Utility;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public abstract class DotPartyParser : ParameterizedHtmlParser
{
    private static readonly string[] ParsableSites = ["drive.google.com", "mega.nz", "sendvid.com", "dropbox.com"];
    
    protected DotPartyParser(WebDriver driver, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, filenameScheme)
    {
    }
    
    /// <summary>
    ///     Parses the html for kemono.su and coomer.su and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <param name="domainUrl">The domain url of the site</param>
    /// <returns></returns>
    protected async Task<RipInfo> DotPartyParse(string domainUrl)
    {
        var baseUrl = CurrentUrl;
        var urlSplit = baseUrl.Split("/");
        var sourceSite = urlSplit[3];
        baseUrl = string.Join("/", urlSplit[3..6]).Split("?")[0];
        baseUrl = $"{domainUrl}/api/v1/{baseUrl}";

        using var client = new HttpClient();
        var response = await client.GetAsync($"{baseUrl}/profile");
        if (!response.IsSuccessStatusCode)
        {
            throw new RipperException("Failed to get profile page");
        }

        var json = await response.Content.ReadFromJsonAsync<JsonNode>();
        var dirName = json!.AsObject()["name"]!.Deserialize<string>()!;

        // await WaitForElement("//h1[@id='user-header__info-top']");
        // var soup = await SolveParseAddCookies();
        // var dirName = soup.SelectSingleNode("//h1[@id='user-header__info-top']")
        //                   .SelectSingleNode(".//span[@itemprop='name']").InnerText;
        dirName = $"{dirName} - ({sourceSite})";

        #region Get All Posts

        var posts = new List<JsonObject>();
        var page = 0;
        while (true)
        {
            response = await client.GetAsync($"{baseUrl}?o={page * 50}");
            page++;
            if (!response.IsSuccessStatusCode)
            {
                throw new RipperException($"Failed to get page {page}");
            }

            json = await response.Content.ReadFromJsonAsync<JsonNode>();
            var jsonPosts = json!.AsArray();
            posts.AddRange(jsonPosts.Select(post => post!.AsObject()));
            if (jsonPosts.Count < 50)
            {
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

        foreach (var (i, post) in posts.Enumerate())
        {
            Log.Information("Parsing post {PostNum} of {TotalPosts}", i + 1, numPosts);
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
            var name = file["name"]!.Deserialize<string>()!;
            var path = file["path"]?.Deserialize<string>();
            if (path is not null)
            {
                if (path[0] == '/')
                {
                    path = domainUrl + path;
                }

                var imageLink = new ImageLink(path, FilenameScheme, 0, filename: name);
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

            // var imageListContainer = soup.SelectSingleNode("//div[@class='post__files']");
            // if (imageListContainer is null)
            // {
            //     continue;
            // }
            //
            // var imageList = imageListContainer.SelectNodes("//a[@class='fileThumb image-link']");
            // var imageListLinks = imageList.GetHrefs();
            // images.AddRange(imageListLinks);
        }

        #endregion

        foreach (var site in ExternalSites)
        {
            externalLinks[site] = externalLinks[site].RemoveDuplicates();
        }

        SaveExternalLinks(externalLinks);
        //images = images.RemoveDuplicates(); // Handled in RipInfo.ConvertUrlsToImageLink
        var stringLinks = new List<StringImageLinkWrapper>();
        foreach (var link in images)
        {
            if (!link.Contains("dropbox.com/"))
            {
                stringLinks.Add(link);
            }
            else
            {
                var dropboxParser = new DropboxParser(WebDriver, RequestHeaders, FilenameScheme);
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