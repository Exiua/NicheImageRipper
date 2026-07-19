using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Exceptions;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers;

public class PixelDrainParser : ParameterizedHtmlParser, IHtmlParser
{
    public static string ParserName => "pixeldrain";

    public PixelDrainParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders,
                            FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager,
        requestHeaders, IHtmlParser.GetFilenameScheme<PixelDrainParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the HTML for pixeldrain.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(url))
        {
            url = CurrentUrl;
        }

        var apiKey = Config.Keys.Pixeldrain;
        var counter = 0;
        var images = new List<StringFileLinkWrapper>();
        string dirName;
        if (url.Contains("/l/"))
        {
            var id = url.Split("/")[4].Split("#")[0];
            var response = await HttpClient.GetAsync($"https://pixeldrain.com/api/list/{id}", cancellationToken);
            var responseJson = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: cancellationToken);
            dirName = responseJson!["title"]!.Deserialize<string>()!;
            var files = responseJson["files"]!.AsArray();
            foreach (var file in files)
            {
                var link = FileLink.WithFilename(file!["id"]!.Deserialize<string>()!,
                    file["name"]!.Deserialize<string>()!, FilenameScheme, counter, linkInfo: LinkInfo.PixelDrain);
                counter++;
                images.Add(link);
            }
        }
        else if (url.Contains("/u/"))
        {
            var id = url.Split("/")[4];
            var response = await HttpClient.GetAsync($"https://pixeldrain.com/api/file/{id}/info", cancellationToken);
            var responseJson = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: cancellationToken);
            dirName = responseJson!["id"]!.Deserialize<string>()!;
            var link = FileLink.WithFilename(responseJson!["id"]!.Deserialize<string>()!,
                responseJson["name"]!.Deserialize<string>()!, FilenameScheme, counter, linkInfo: LinkInfo.PixelDrain);
            images.Add(link);
        }
        else
        {
            throw new RipperException($"Unknown url: {url}");
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}