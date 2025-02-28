using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Core.DataStructures;
using Core.Enums;
using Core.Exceptions;
using Core.ExtensionMethods;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class PixelDrainParser : HtmlParser
{
    public PixelDrainParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for site and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override Task<RipInfo> Parse()
    {
        return PixelDrainParse("");
    }

    /// <summary>
    ///     Parses the html for site and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> PixelDrainParse(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            url = CurrentUrl;
        }
        var apiKey = Config.Keys["Pixeldrain"];
        var counter = 0;
        var images = new List<StringImageLinkWrapper>();
        string dirName;
        var client = new HttpClient();
        if (url.Contains("/l/"))
        {
            var id = url.Split("/")[4].Split("#")[0];
            var response = await client.GetAsync($"https://pixeldrain.com/api/list/{id}");
            var responseJson = await response.Content.ReadFromJsonAsync<JsonNode>();
            dirName = responseJson!["title"]!.Deserialize<string>()!;
            var files = responseJson["files"]!.AsArray();
            foreach (var file in files)
            {
                var link = new ImageLink(file!["id"]!.Deserialize<string>()!, FilenameScheme, counter,
                    filename: file["name"]!.Deserialize<string>()!, linkInfo: LinkInfo.PixelDrain);
                counter++;
                images.Add(link);
            }
        }
        else if (url.Contains("/u/"))
        {
            var id = url.Split("/")[4];
            var response = await client.GetAsync($"https://pixeldrain.com/api/file/{id}/info");
            var responseJson = await response.Content.ReadFromJsonAsync<JsonNode>();
            dirName = responseJson!["id"]!.Deserialize<string>()!;
            var link = new ImageLink(responseJson["id"]!.Deserialize<string>()!, FilenameScheme, counter,
                filename: responseJson["name"]!.Deserialize<string>()!, linkInfo: LinkInfo.PixelDrain);
            images.Add(link);
        }
        else
        {
            throw new RipperException($"Unknown url: {url}");
        }
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
