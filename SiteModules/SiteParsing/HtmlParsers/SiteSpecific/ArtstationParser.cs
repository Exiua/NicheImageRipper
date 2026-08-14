using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using NicheImageRipper.Core.Exceptions;
using NicheImageRipper.Core.SiteParsing.HtmlParsers;
using OpenQA.Selenium;
using HtmlAgilityPack;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.SiteParsing.HtmlParsers.SiteSpecific;
public class ArtstationParser : HtmlParser
{
    public static string ParserName => "artstation";
    public static string[] SupportedUrls { get; } = ["https://www.artstation.com/"];

    public ArtstationParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    // TODO: Fix this
    /// <summary>
    ///     Parses  the HTML for artstation.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='artist-name']").InnerText;
        var username = CurrentUrl.Split("/")[3];
        var cacheScript = soup.SelectSingleNodeOrThrow("//div[@class='wrapper-main']").SelectNodesOrThrow(".//script")[1].InnerText;
#region Id Extraction
        var start = cacheScript.IndexOf("quick.json", StringComparison.Ordinal);
        var end = cacheScript.LastIndexOf(");", StringComparison.Ordinal);
        var jsonData = cacheScript[(start + 14)..(end - 1)].Replace("\n", "").Replace('\"', '"');
        var json = JsonSerializer.Deserialize<JsonNode>(jsonData);
        var userId = json!["id"]!.Deserialize<string>()!;
        var userName = json["full_name"]!.Deserialize<string>()!;
#endregion
#region Get Posts
        var total = 1;
        var pageCount = 1;
        var firstIter = true;
        var posts = new List<string>();
        var client = new HttpClient();
        while (total > 0)
        {
            Logger.Information("Page {PageCount} of {Total}", pageCount, total);
            var url = $"https://www.artstation.com/users/{username}/projects.json?page={pageCount}";
            Logger.Debug("Requesting {Url}", url);
            var response = await client.GetAsync(url);
            var responseData = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: cancellationToken);
            var data = responseData!["data"]!.AsArray();
            foreach (var d in data)
            {
                var link = d!["permalink"]!.Deserialize<string>()!.Split("/")[4];
                posts.Add(link);
            }

            if (firstIter)
            {
                total = responseData["total_count"]!.Deserialize<int>() - data.Count;
                firstIter = false;
            }
            else
            {
                total -= data.Count;
            }

            pageCount += 1;
            await Sleep(100, cancellationToken: cancellationToken);
        }

#endregion
#region Get Media Links
        var images = new List<StringFileLinkWrapper>();
        foreach (var post in posts)
        {
            var url = $"https://www.artstation.com/projects/{post}.json";
            HttpResponseMessage response;
            try
            {
                response = await client.GetAsync(url);
            }
            catch (HttpRequestException)
            {
                await Sleep(5000, cancellationToken: cancellationToken);
                response = await client.GetAsync(url);
            }

            var responseData = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: cancellationToken);
            var assets = responseData!["assets"]!.AsArray();
            var urls = assets.Select(asset => asset!["image_url"]!.Deserialize<string>()!.Replace("/large/", "/4k/"));
            images.AddRange(urls.ToStringImageLinks());
        }

#endregion
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}