using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Core.Configuration;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class RedGifsParser : HtmlParser
{
    public RedGifsParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for redgifs.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        await Task.Delay(3000);
        const string baseRequest = "https://api.redgifs.com/v2/gifs?ids=";
        await LazyLoad(scrollBy: true, increment: 1250);
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//h1[@class='userName']").InnerText;
        var images = new List<StringImageLinkWrapper>();
        var posts = soup.SelectSingleNode("//div[@class='tileFeed']").SelectNodes("./a[@href]").GetHrefs();
        var ids = posts.Select(post => post.Split("/")[^1].Split("#")[0]).ToList();
        var idChunks = ids.Chunk(100);
        var session = new HttpClient();
        var token = await TokenManager.Instance.GetToken("redgifs");
        RequestHeaders["Authorization"] = $"Bearer {token.Value}";
        foreach (var chunk in idChunks)
        {
            var idParam = string.Join("%2C", chunk);
            var request = RequestHeaders.ToRequest(HttpMethod.Get, $"{baseRequest}{idParam}");
            var response = await session.SendAsync(request);
            var responseJson = (await response.Content.ReadFromJsonAsync<JsonNode>())!;
            var gifs = responseJson["gifs"]!.AsArray();
            images.AddRange(gifs
                            .Select(gif => gif!["urls"]!["hd"]!
                                .Deserialize<string>())
                            .Select(gifUrl => gifUrl!)
                            .Select(dummy => (StringImageLinkWrapper)dummy));
        }
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
