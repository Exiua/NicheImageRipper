using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;


using Sdk.DataStructures;
using Sdk.Enums;
using Sdk.Exceptions;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.Imgur;
public class ImgurParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "imgur";
    public static string[] SupportedUrls => ["https://imgur.com/"];

    public ImgurParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<ImgurParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for imgur.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var clientId = Config.Keys.GetValueOrDefault(ParserName, "");
        if (clientId == "")
        {
            Logger.Error("Client Id not set");
            Logger.Error("Follow to generate Client Id: https://apidocs.imgur.com/#intro");
            Logger.Error("Then add Client Id to Imgur in config.json under Keys");
            throw new RipperCredentialException("Client Id Not Set");
        }

        RequestHeaders["Authorization"] = "Client-ID " + clientId;
        var albumHash = CurrentUrl.Split("/")[5];
        var session = new HttpClient();
        var request = RequestHeaders.ToRequest(HttpMethod.Get, $"https://api.imgur.com/3/album/{albumHash}");
        var response = await session.SendAsync(request);
        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            Logger.Error("Client Id is incorrect");
            throw new RipperCredentialException("Client Id Incorrect");
        }

        var json = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: cancellationToken);
        var jsonData = json!["data"]!.AsObject();
        var dirName = jsonData["title"]!.Deserialize<string>()!;
        var images = jsonData["images"]!.AsArray().Select(img => img!["link"]!.Deserialize<string>()!).ToList();
        return RipInfo.FromUrlList(images.ToStringFileLinkWrapperList(), dirName, FilenameScheme);
    }
}