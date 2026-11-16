using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NicheImageRipper.Sdk.Common.ExtensionMethods;
using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.Luscious;

public class LusciousParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "luscious";
    public static string[] SupportedUrls => ["https://www.luscious.net/"];

    public LusciousParser(WebDriver driver, Dictionary<string, string> requestHeaders,
                          FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders,
        IHtmlParser.GetFilenameScheme<LusciousParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the HTML for luscious.net and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        if (CurrentUrl.Contains("members."))
        {
            CurrentUrl = CurrentUrl.Replace("members.", "www.");
        }

        var soup = await Soupify(cancellationToken: cancellationToken);
        var dirName = soup
                     .SelectSingleNodeOrThrow(
                          "//h1[@class='o-h1 album-heading']|//h1[@class='o-h1 video-heading o-padding-sides']")
                     .InnerText;
        const string endpoint = "https://members.luscious.net/graphqli/?";
        var albumId = CurrentUrl.Split("/")[4].Split("_")[^1];
        List<StringFileLinkWrapper> images = [];
        Dictionary<string, object> variables;
        string query;
        if (CurrentUrl.Contains("/videos/"))
        {
            variables = new Dictionary<string, object>
            {
                ["id"] = albumId
            };
            query = """
                    query getVideoInfo($id: ID!) {
                        video {
                            get(id: $id) {
                            ... on Video {...VideoStandard}
                            ... on MutationError {errors {code message}}
                            }
                        }
                    }
                    fragment VideoStandard on Video{id title tags content genres description audiences url poster_url subtitle_url v240p v360p v720p v1080p}
                    """;
            var response = await HttpClient.PostAsync(endpoint,
                new StringContent(JsonSerializer.Serialize(new { operationName = "getVideoInfo", query, variables }),
                    Encoding.UTF8, "application/json"), cancellationToken);
            var json = await response.Content.ReadFromJsonAsync<GraphQlResponse<VideoData>>(
                cancellationToken: cancellationToken);
            var jsonData = json!.Data.Video.Get;
            var videoUrl = jsonData.V1080p ?? jsonData.V720p ?? jsonData.V360p ?? jsonData.V240p!;
            images.Add(videoUrl);
        }
        else
        {
            variables = new Dictionary<string, object>
            {
                ["input"] = new Dictionary<string, object>
                {
                    ["page"] = 1,
                    ["display"] = "date_newest",
                    ["filters"] = new List<Dictionary<string, string>>
                    {
                        new()
                        {
                            ["name"] = "album_id",
                            ["value"] = albumId
                        }
                    }
                }
            };
            query = """
                    query PictureQuery($input: PictureListInput!) {
                        picture {
                            list(input: $input) {
                                info {
                                    total_items
                                    has_next_page
                                }
                                items {
                                    id
                                    title
                                    url_to_original
                                    tags{
                                        id
                                        text
                                    }
                                }
                            }
                        }
                    }
                    """;
            var nextPage = true;
            while (nextPage)
            {
                var response = await HttpClient.PostAsync(endpoint,
                    new StringContent(
                        JsonSerializer.Serialize(new { operationName = "PictureQuery", query, variables }),
                        Encoding.UTF8, "application/json"), cancellationToken);
                var json = await response.Content.ReadFromJsonAsync<GraphQlResponse<PictureData>>(
                    cancellationToken: cancellationToken);
                var jsonData = json!.Data.Picture.List;
                nextPage = jsonData.Info.HasNextPage;
                var inputDict = (Dictionary<string, object>)variables["input"];
                inputDict["page"] = (int)inputDict["page"] + 1;
                images.AddRange(jsonData.Items.Select(item => (StringFileLinkWrapper)item.UrlToOriginal));
            }
        }

        return await RipInfo.FromUrlListAsync(images, dirName, FilenameScheme);
    }

    private class GraphQlResponse<T>
    {
        [JsonPropertyName("data")]
        public T Data { get; set; } = default!;
    }

    private class VideoData
    {
        [JsonPropertyName("video")]
        public VideoQuery Video { get; set; } = null!;
    }

    private class VideoQuery
    {
        [JsonPropertyName("get")]
        public Video Get { get; set; } = null!;
    }

    private class Video
    {
        [JsonPropertyName("v240p")]
        public string? V240p { get; set; }

        [JsonPropertyName("v360p")]
        public string? V360p { get; set; }

        [JsonPropertyName("v720p")]
        public string? V720p { get; set; }

        [JsonPropertyName("v1080p")]
        public string? V1080p { get; set; }
    }

    private class PictureData
    {
        [JsonPropertyName("picture")]
        public PictureQuery Picture { get; set; } = null!;
    }

    private class PictureQuery
    {
        [JsonPropertyName("list")]
        public PictureList List { get; set; } = null!;
    }

    private class PictureList
    {
        [JsonPropertyName("info")]
        public PageInfo Info { get; set; } = null!;

        [JsonPropertyName("items")]
        public List<Picture> Items { get; set; } = null!;
    }

    private class PageInfo
    {
        [JsonPropertyName("has_next_page")]
        public bool HasNextPage { get; set; }
    }

    private class Picture
    {
        [JsonPropertyName("url_to_original")]
        public string UrlToOriginal { get; set; } = null!;
    }
}