using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class LusciousParser : HtmlParser
{
    public LusciousParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for luscious.net and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        if (CurrentUrl.Contains("members."))
        {
            CurrentUrl = CurrentUrl.Replace("members.", "www.");
        }
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@class='o-h1 album-heading']|//h1[@class='o-h1 video-heading o-padding-sides']").InnerText;
        const string endpoint = "https://members.luscious.net/graphqli/?";
        var albumId = CurrentUrl.Split("/")[4].Split("_")[^1];
        var session = new HttpClient();
        List<StringImageLinkWrapper> images = [];
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
            var response = await session.PostAsync(endpoint, new StringContent(JsonSerializer.Serialize(new
            {
                operationName = "getVideoInfo",
                query,
                variables
            }), Encoding.UTF8, "application/json"));
            var json = await response.Content.ReadFromJsonAsync<JsonNode>();
            var jsonData = json!["data"]!["video"]!["get"]!;
            string videoUrl;
            if(!jsonData["v1080p"].IsNull())
            {
                videoUrl = jsonData["v1080p"]!.Deserialize<string>()!;
            }
            else if(!jsonData["v720p"].IsNull())
            {
                videoUrl = jsonData["v720p"]!.Deserialize<string>()!;
            }
            else if(!jsonData["v360p"].IsNull())
            {
                videoUrl = jsonData["v360p"]!.Deserialize<string>()!;
            }
            else
            {
                videoUrl = jsonData["v240p"]!.Deserialize<string>()!;
            }
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
                var response = await session.PostAsync(endpoint, new StringContent(JsonSerializer.Serialize(new
                {
                    operationName = "PictureQuery",
                    query,
                    variables
                }), Encoding.UTF8, "application/json"));
                var json = await response.Content.ReadFromJsonAsync<JsonNode>();
                var jsonData = json!["data"]!["picture"]!["list"]!;
                nextPage = jsonData["info"]!["has_next_page"]!.Deserialize<bool>();
                var inputDict = (Dictionary<string, object>)variables["input"];
                inputDict["page"] = (int)inputDict["page"] + 1;
                var items = jsonData["items"]!.AsArray();
                images.AddRange(items.Select(item => (StringImageLinkWrapper)item!["url_to_original"]!.Deserialize<string>()!));
            }
        }
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
