using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using NicheImageRipper.Sdk.Common.ExtensionMethods;
using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.Exceptions;
using NicheImageRipper.Sdk.SiteParsing;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.Imgur;

public class ImgurParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "imgur";
    public static string[] SupportedUrls => ["https://imgur.com/"];

    public ImgurParser(WebDriver driver, Dictionary<string, string> requestHeaders,
                       FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders,
        IHtmlParser.GetFilenameScheme<ImgurParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for imgur.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var clientId = Config.GetSiteConfig(ParserName)?.Key ?? "";
        if (clientId == "")
        {
            Logger.Error("Client Id not set");
            Logger.Error("Follow to generate Client Id: https://apidocs.imgur.com/#intro");
            Logger.Error("Then add Client Id to Imgur in config.json under Keys");
            throw new RipperCredentialException("Client Id Not Set");
        }

        RequestHeaders["Authorization"] = "Client-ID " + clientId;
        var albumHash = CurrentUrl.Split("/")[5];
        var request = RequestHeaders.ToRequest(HttpMethod.Get, $"https://api.imgur.com/3/album/{albumHash}");
        var response = await HttpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            Logger.Error("Client Id is incorrect");
            throw new RipperCredentialException("Client Id Incorrect");
        }

        var json = await response.Content.ReadFromJsonAsync<AlbumResponse>(cancellationToken: cancellationToken);
        var jsonData = json!.Data;
        var dirName = jsonData.Title;
        var images = jsonData.Images.Select(img => img.Link).ToList();
        return await RipInfo.FromUrlListAsync(images.ToStringFileLinkWrapperList(), dirName, FilenameScheme);
    }

    private class AlbumResponse
    {
        [JsonPropertyName("data")]
        public Album Data { get; set; } = null!;
    }

    private class Album
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = null!;

        [JsonPropertyName("images")]
        public List<AlbumImage> Images { get; set; } = null!;
    }

    private class AlbumImage
    {
        [JsonPropertyName("link")]
        public string Link { get; set; } = null!;
    }
}