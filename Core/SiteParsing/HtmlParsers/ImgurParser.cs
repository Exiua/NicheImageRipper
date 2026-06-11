using Common.ExtensionMethods;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Core.Configuration;
using Core.DataStructures;
using Core.Enums;
using Core.Exceptions;
using Core.ExtensionMethods;
using Core.Managers;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class ImgurParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "imgur";
    
    public ImgurParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<ImgurParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for imgur.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var clientId = Config.Keys.Imgur;
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
    
        var json = await response.Content.ReadFromJsonAsync<JsonNode>();
        var jsonData = json!["data"]!.AsObject();
        var dirName = jsonData["title"]!.Deserialize<string>()!;
        var images = jsonData["images"]!.AsArray().Select(img => img!["link"]!.Deserialize<string>()!).ToList();
    
        return RipInfo.FromUrlList(images.ToStringImageLinkWrapperList(), dirName, FilenameScheme);
    }
}
