using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;


using OpenQA.Selenium;
using Sdk.Common.ExtensionMethods;
using Sdk.DataStructures;
using Sdk.Enums;
using Sdk.Exceptions;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.OmegaScans;
public class OmegaScansParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "omegascans";
    public static string[] SupportedUrls => ["https://omegascans.org/"];

    public OmegaScansParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<OmegaScansParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the HTML for omegascans.org and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns></returns>
    public override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        const string chapterListXpath = "//ul[contains(concat(' ', normalize-space(@class), ' '), ' grid ')]";
        var lazyLoadArgs = new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 2500,
            ScrollPauseTime = 750,
        };
        var(capturer, b) = await ConfigureNetworkCapture<OmegaScansVideoCapturer>(cancellationToken: cancellationToken);
        await using var bidi = b;
        Driver.Refresh();
        Logger.Debug("Waiting for chapter list to load");
        var soup = await Soupify(xpath: chapterListXpath, xpathTimeout: 60, lazyLoadArgs: lazyLoadArgs, cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//h1").InnerText;
        var chapterCountStr = soup.SelectSingleNodeOrThrow("//span[normalize-space(.)='Total chapters']/following-sibling::span").InnerText;
        var chapterCount = int.Parse(chapterCountStr.Trim().Split(' ')[0]);
        Logger.Debug("Found {chapterCount} chapters", chapterCount);
        var apiUrl = "";
        await WaitForPlaylist(capturer, links =>
        {
            var url = links[0];
            apiUrl = url;
        }, cancellationToken: cancellationToken);
        if (apiUrl == "")
        {
            Logger.Error("No API URL found from network capture");
            throw new RipperException("Failed to capture API URL for OmegaScans.");
        }

        Logger.Debug("Captured API URL: {apiUrl}", apiUrl);
        var seriesId = apiUrl.Split("series_id=")[1].Split("&")[0];
        Logger.Debug("Extracted series ID: {seriesId}", seriesId);
        using var client = new HttpClient();
        List<string> chapters = [];
        var page = 1;
        while (true)
        {
            Logger.Information("Fetching chapter list page {page}", page);
            var response = await client.GetAsync($"https://api.omegascans.org/chapter/query?page={page}&perPage=30&series_id={seriesId}");
            page++;
            response.EnsureSuccessStatusCode();
            var chapterResponse = await response.Content.ReadFromJsonAsync<GetChapterResponse>(cancellationToken: cancellationToken);
            if (chapterResponse is null)
            {
                Logger.Error("Failed to deserialize chapter response");
                throw new RipperException("Failed to parse chapter list from OmegaScans API.");
            }

            if (chapterResponse.Data.Count == 0)
            {
                Logger.Information("No more chapters found, ending pagination.");
                break;
            }

            chapters.AddRange(chapterResponse.Data.Select(chapter => $"https://omegascans.org/series/{chapter.Series.SeriesSlug}/{chapter.ChapterSlug}"));
            if (chapterResponse.Metadata.NextPageUrl is null || chapterResponse.Metadata.CurrentPage == chapterResponse.Metadata.LastPage)
            {
                Logger.Information("Reached last page of chapters.");
                break;
            }
        }

        chapters.Reverse();
        var images = new List<StringFileLinkWrapper>();
        foreach (var(i, chapter)in chapters.Enumerate())
        {
            Logger.Information("Parsing chapter {i} of {chapterCount}", i + 1, chapterCount);
            while (true)
            {
                try
                {
                    soup = await Soupify(chapter, lazyLoadArgs: lazyLoadArgs, cancellationToken: cancellationToken);
                    break;
                }
                catch (WebDriverException)
                {
                    Logger.Warning("WebDriverException encountered, retrying...");
                    await Sleep(1000, cancellationToken: cancellationToken);
                    WebDriver.RegenerateDriver();
                }
            }

            var post = soup.SelectSingleNode("//div[@class='container']");
            if (post is null)
            {
                Logger.Warning("Post not found");
                continue;
            }

            var imgs = post.SelectNodesOrThrow(".//img[@src]").Select(img => img.GetNullableSrc() ?? img.GetAttributeValue("data-src"));
            images.AddRange(imgs.ToStringFileLinks());
            await Sleep(250, cancellationToken: cancellationToken);
        }

        // Files are numbered per chapter, so original will have the files overwrite each other
        return RipInfo.FromUrlList(images, dirName, FilenameScheme, nameReuse: true);
    }

    public class GetChapterResponse
    {
        [JsonPropertyName("meta")]
        public Metadata Metadata { get; set; } = null!;

        [JsonPropertyName("data")]
        public List<ChapterData> Data { get; set; } = null!;
    }

    public class Metadata
    {
        [JsonPropertyName("total")]
        public int Total { get; set; }

        [JsonPropertyName("per_page")]
        public int PerPage { get; set; }

        [JsonPropertyName("current_page")]
        public int CurrentPage { get; set; }

        [JsonPropertyName("last_page")]
        public int LastPage { get; set; }

        [JsonPropertyName("first_page_url")]
        public string FirstPageUrl { get; set; } = null!;

        [JsonPropertyName("last_page_url")]
        public string LastPageUrl { get; set; } = null!;

        [JsonPropertyName("next_page_url")]
        public string? NextPageUrl { get; set; }

        [JsonPropertyName("previous_page_url")]
        public string? PreviousPageUrl { get; set; }
    }

    public class ChapterData
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("chapter_name")]
        public string ChapterName { get; set; } = null!;

        [JsonPropertyName("chapter_title")]
        public string? ChapterTitle { get; set; }

        [JsonPropertyName("chapter_thumbnail")]
        public string ChapterThumbnail { get; set; } = null!;

        [JsonPropertyName("chapter_slug")]
        public string ChapterSlug { get; set; } = null!;

        [JsonPropertyName("price")]
        public int Price { get; set; }

        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; } = null!;

        [JsonPropertyName("series")]
        public SeriesData Series { get; set; } = null!;

        [JsonPropertyName("meta")]
        public ChapterMetadata Meta { get; set; } = null!;
    }

    public class SeriesData
    {
        [JsonPropertyName("series_slug")]
        public string SeriesSlug { get; set; } = null!;

        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("latest_chapter")]
        public JsonNode? LatestChapter { get; set; }

        [JsonPropertyName("meta")]
        public Dictionary<string, JsonNode> Meta { get; set; } = null!;
    }

    public class ChapterMetadata
    {
        [JsonPropertyName("continuation")]
        public JsonNode? Continuation { get; set; }
    }
}