using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.Exceptions;
using Core.ExtensionMethods;
using Core.Managers;
using Core.SiteParsing.VideoCapturers;
using OpenQA.Selenium;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class OmegaScansParser : HtmlParser
{
    public OmegaScansParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for omegascans.org and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns></returns>
    protected override async Task<RipInfo> Parse()
    {
        const string chapterListXpath = "//ul[contains(concat(' ', normalize-space(@class), ' '), ' grid ')]";

        var lazyLoadArgs = new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 2500,
            ScrollPauseTime = 750,
        };

        var (capturer, b) = await ConfigureNetworkCapture<OmegaScansVideoCapturer>();
        await using var bidi = b;
        Driver.Refresh();
        
        Log.Debug("Waiting for chapter list to load");
        var soup = await Soupify(xpath: chapterListXpath, xpathTimout: 60, lazyLoadArgs: lazyLoadArgs);
        var dirName = soup.SelectSingleNodeOrThrow("//h1").InnerText;
        var chapterCountStr = soup.SelectSingleNodeOrThrow("//span[normalize-space(.)='Total chapters']/following-sibling::span")
                                    .InnerText;
        var chapterCount = int.Parse(chapterCountStr.Trim().Split(' ')[0]);
        Log.Debug("Found {chapterCount} chapters", chapterCount);

        var apiUrl = "";
        await WaitForPlaylist(capturer, links =>
        {
            var url = links[0];
            apiUrl = url;
        });

        if (apiUrl == "")
        {
            Log.Error("No API URL found from network capture");
            throw new RipperException("Failed to capture API URL for OmegaScans.");
        }
        
        Log.Debug("Captured API URL: {apiUrl}", apiUrl);
        var seriesId = apiUrl.Split("series_id=")[1].Split("&")[0];
        Log.Debug("Extracted series ID: {seriesId}", seriesId);

        using var client = new HttpClient();
        List<string> chapters = [];
        var page = 1;
        while (true)
        {
            Log.Information("Fetching chapter list page {page}", page);
            var response =
                await client.GetAsync(
                    $"https://api.omegascans.org/chapter/query?page={page}&perPage=30&series_id={seriesId}");
            page++;
            response.EnsureSuccessStatusCode();
            var chapterResponse = await response.Content.ReadFromJsonAsync<GetChapterResponse>();
            if (chapterResponse is null)
            {
                Log.Error("Failed to deserialize chapter response");
                throw new RipperException("Failed to parse chapter list from OmegaScans API.");
            }
            
            if (chapterResponse.Data.Count == 0)
            {
                Log.Information("No more chapters found, ending pagination.");
                break;
            }

            chapters.AddRange(chapterResponse.Data.Select(chapter => $"https://omegascans.org/series/{chapter.Series.SeriesSlug}/{chapter.ChapterSlug}"));

            if (chapterResponse.Metadata.NextPageUrl is null ||
                chapterResponse.Metadata.CurrentPage == chapterResponse.Metadata.LastPage)
            {
                Log.Information("Reached last page of chapters.");
                break;
            }
        }
    
        chapters.Reverse();
        var images = new List<StringImageLinkWrapper>();
        foreach (var (i, chapter) in chapters.Enumerate())
        {
            Log.Information("Parsing chapter {i} of {chapterCount}", i + 1, chapterCount);
            while (true)
            {
                try
                {
                    soup = await Soupify(chapter, lazyLoadArgs: lazyLoadArgs);
                    break;
                }
                catch (WebDriverException)
                {
                    Log.Warning("WebDriverException encountered, retrying...");
                    await Sleep(1000);
                    WebDriver.RegenerateDriver();
                }
            }
            
            var post = soup.SelectSingleNode("//div[@class='container']");
            if (post is null)
            {
                Log.Warning("Post not found");
                continue;
            }
    
            var imgs = post.SelectNodesOrThrow(".//img[@src]")
                           .Select(img => img.GetNullableSrc() ?? img.GetAttributeValue("data-src"));
            images.AddRange(imgs.ToStringImageLinks());
            await Sleep(250);
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
