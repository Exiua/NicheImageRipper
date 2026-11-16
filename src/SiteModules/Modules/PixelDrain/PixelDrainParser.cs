using System.Net.Http.Json;
using System.Text.Json.Serialization;
using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.Exceptions;
using NicheImageRipper.Sdk.SiteParsing;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.PixelDrain;

public class PixelDrainParser : ParameterizedHtmlParser, IHtmlParser
{
    public static string ParserName => "pixeldrain";

    public static string[] SupportedUrls { get; } = ["https://pixeldrain.com/"];

    protected override bool RequiresNavigation => false;

    public PixelDrainParser(WebDriver driver,  Dictionary<string, string> requestHeaders,
                            FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, 
        requestHeaders, IHtmlParser.GetFilenameScheme<PixelDrainParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the HTML for pixeldrain.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> ParseCore(CancellationToken cancellationToken = default)
    {
        var url = GivenUrl;
        var apiKey = Config.GetSiteConfig(ParserName)?.Key ?? "";
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new RipperException("No API key found for " + ParserName);
        }

        var counter = 0;
        var images = new List<StringFileLinkWrapper>();
        string dirName;
        if (url.Contains("/l/"))
        {
            var id = url.Split("/")[4].Split("#")[0];
            var response = await HttpClient.GetAsync($"https://pixeldrain.com/api/list/{id}", cancellationToken);
            var responseJson = await response.Content.ReadFromJsonAsync<FileList>(cancellationToken: cancellationToken);
            dirName = responseJson!.Title;
            foreach (var file in responseJson.Files)
            {
                var link = FileLink.WithFilename(file.Id, file.Name, FilenameScheme, counter,
                    linkInfo: PixelDrainLinkInfo.PixelDrain);
                counter++;
                images.Add(link);
            }
        }
        else if (url.Contains("/u/"))
        {
            var id = url.Split("/")[4];
            var response = await HttpClient.GetAsync($"https://pixeldrain.com/api/file/{id}/info", cancellationToken);
            var responseJson = await response.Content.ReadFromJsonAsync<PixelDrainFile>(cancellationToken: cancellationToken);
            dirName = responseJson!.Id;
            var link = FileLink.WithFilename(responseJson.Id, responseJson.Name, FilenameScheme, counter,
                linkInfo: PixelDrainLinkInfo.PixelDrain);
            images.Add(link);
        }
        else
        {
            throw new RipperException($"Unknown url: {url}");
        }

        return await RipInfo.FromUrlListAsync(images, dirName, FilenameScheme);
    }

    private class FileList
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = null!;

        [JsonPropertyName("files")]
        public List<PixelDrainFile> Files { get; set; } = null!;
    }

    private class PixelDrainFile
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = null!;

        [JsonPropertyName("name")]
        public string Name { get; set; } = null!;
    }
}