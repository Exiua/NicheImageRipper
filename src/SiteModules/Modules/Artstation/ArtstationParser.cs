using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using NicheImageRipper.Sdk.Common.ExtensionMethods;
using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.Artstation;

public class ArtstationParser : HtmlParser
{
    public static string ParserName => "artstation";
    public static string[] SupportedUrls { get; } = ["https://www.artstation.com/"];

    public ArtstationParser(WebDriver driver, Dictionary<string, string> requestHeaders,
                            FilenameScheme filenameScheme = Sdk.Enums.FilenameScheme.Original) : base(driver,
        requestHeaders, filenameScheme)
    {
    }

    // TODO: Fix this
    /// <summary>
    ///     Parses  the HTML for artstation.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='artist-name']").InnerText;
        var username = CurrentUrl.Split("/")[3];
        var cacheScript =
            soup.SelectSingleNodeOrThrow("//div[@class='wrapper-main']").SelectNodesOrThrow(".//script")[1].InnerText;

        #region Id Extraction

        var start = cacheScript.IndexOf("quick.json", StringComparison.Ordinal);
        var end = cacheScript.LastIndexOf(");", StringComparison.Ordinal);
        var jsonData = cacheScript[(start + 14)..(end - 1)].Replace("\n", "").Replace('\"', '"');
        var json = JsonSerializer.Deserialize<UserData>(jsonData)!;
        var userId = json.Id;
        var userName = json.FullName;

        #endregion

        #region Get Posts

        var total = 1;
        var pageCount = 1;
        var firstIter = true;
        var posts = new List<string>();
        var client = RateLimitingHandler.CreateClient();
        while (total > 0)
        {
            Logger.Information("Page {PageCount} of {Total}", pageCount, total);
            var url = $"https://www.artstation.com/users/{username}/projects.json?page={pageCount}";
            Logger.Debug("Requesting {Url}", url);
            var response = await client.GetAsync(url);
            var responseData = await response.Content.ReadFromJsonAsync<ProjectsPage>(cancellationToken: cancellationToken);
            var data = responseData!.Data;
            foreach (var d in data)
            {
                var link = d.Permalink.Split("/")[4];
                posts.Add(link);
            }

            if (firstIter)
            {
                total = responseData.TotalCount - data.Count;
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
                response = await client.GetAsync(url, cancellationToken);
            }
            catch (HttpRequestException)
            {
                await Sleep(5000, cancellationToken: cancellationToken);
                response = await client.GetAsync(url, cancellationToken);
            }

            var responseData = await response.Content.ReadFromJsonAsync<Project>(cancellationToken: cancellationToken);
            var urls = responseData!.Assets.Select(asset => asset.ImageUrl.Replace("/large/", "/4k/"));
            images.AddRange(urls.ToStringFileLinks());
        }

        #endregion

        return await RipInfo.FromUrlListAsync(images, dirName, FilenameScheme);
    }

    private class UserData
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = null!;

        [JsonPropertyName("full_name")]
        public string FullName { get; set; } = null!;
    }

    private class ProjectsPage
    {
        [JsonPropertyName("data")]
        public List<ProjectSummary> Data { get; set; } = null!;

        [JsonPropertyName("total_count")]
        public int TotalCount { get; set; }
    }

    private class ProjectSummary
    {
        [JsonPropertyName("permalink")]
        public string Permalink { get; set; } = null!;
    }

    private class Project
    {
        [JsonPropertyName("assets")]
        public List<Asset> Assets { get; set; } = null!;
    }

    private class Asset
    {
        [JsonPropertyName("image_url")]
        public string ImageUrl { get; set; } = null!;
    }
}