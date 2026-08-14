using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.SiteParsing.HtmlParsers.SiteSpecific;
public class RedGifsParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "redgifs";
    public static string[] SupportedUrls => ["https://www.redgifs.com/"];

    public RedGifsParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<RedGifsParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for redgifs.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        await Sleep(3000);
        const string baseRequest = "https://api.redgifs.com/v2/gifs?ids=";
        await LazyLoad(scrollBy: true, increment: 1250);
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='userName']").InnerText;
        var images = new List<StringFileLinkWrapper>();
        var posts = soup.SelectSingleNodeOrThrow("//div[@class='tileFeed']").SelectNodesOrThrow("./a[@href]").GetHrefs();
        var ids = posts.Select(post => post.Split("/")[^1].Split("#")[0]).ToList();
        var idChunks = ids.Chunk(100);
        var session = new HttpClient();
        var token = await TokenManager.Instance.GetToken(TokenKey.Redgifs);
        RequestHeaders["Authorization"] = $"Bearer {token.Value}";
        foreach (var chunk in idChunks)
        {
            var idParam = string.Join("%2C", chunk);
            var request = RequestHeaders.ToRequest(HttpMethod.Get, $"{baseRequest}{idParam}");
            var response = await session.SendAsync(request);
            var responseJson = (await response.Content.ReadFromJsonAsync<JsonNode>())!;
            var gifs = responseJson["gifs"]!.AsArray();
            images.AddRange(gifs.Select(gif => gif!["urls"]!["hd"]!.Deserialize<string>()).Select(gifUrl => gifUrl!).Select(dummy => (StringFileLinkWrapper)dummy));
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}