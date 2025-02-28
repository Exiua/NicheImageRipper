using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Core.DataStructures;
using Core.Enums;
using Core.Exceptions;
using Core.ExtensionMethods;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class ImgurParser : HtmlParser
{
    public ImgurParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for imgur.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var clientId = Config.Keys["Imgur"];
        if (clientId == "")
        {
            Log.Error("Client Id not set");
            Log.Error("Follow to generate Client Id: https://apidocs.imgur.com/#intro");
            Log.Error("Then add Client Id to Imgur in config.json under Keys");
            throw new RipperCredentialException("Client Id Not Set");
        }
    
        RequestHeaders["Authorization"] = "Client-ID " + clientId;
        var albumHash = CurrentUrl.Split("/")[5];
        var session = new HttpClient();
        var request = RequestHeaders.ToRequest(HttpMethod.Get, $"https://api.imgur.com/3/album/{albumHash}");
        var response = await session.SendAsync(request);
        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            Log.Error("Client Id is incorrect");
            throw new RipperCredentialException("Client Id Incorrect");
        }
    
        var json = await response.Content.ReadFromJsonAsync<JsonNode>();
        var jsonData = json!["data"]!.AsObject();
        var dirName = jsonData["title"]!.Deserialize<string>()!;
        var images = jsonData["images"]!.AsArray().Select(img => img!["link"]!.Deserialize<string>()!).ToList();
    
        return new RipInfo(images.ToStringImageLinkWrapperList(), dirName, FilenameScheme);
    }
}
