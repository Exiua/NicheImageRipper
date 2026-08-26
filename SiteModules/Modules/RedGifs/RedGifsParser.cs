using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using NicheImageRipper.Sdk.Common.ExtensionMethods;
using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using NicheImageRipper.Sdk.TokenManagement;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.RedGifs;

public class RedGifsParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "redgifs";
    public static string[] SupportedUrls => ["https://www.redgifs.com/"];
    
    private static readonly HttpClient Client = GenerateHttpClient();

    public RedGifsParser(WebDriver driver, Dictionary<string, string> requestHeaders,
                         FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders,
        IHtmlParser.GetFilenameScheme<RedGifsParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for redgifs.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        await Sleep(3000, cancellationToken: cancellationToken);
        const string baseRequest = "https://api.redgifs.com/v2/gifs?ids=";
        await LazyLoad(scrollBy: true, increment: 1250, cancellationToken: cancellationToken);
        var soup = await Soupify(cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='userName']").InnerText;
        var images = new List<StringFileLinkWrapper>();
        var posts = soup.SelectSingleNodeOrThrow("//div[@class='tileFeed']").SelectNodesOrThrow("./a[@href]")
                        .GetHrefs();
        var ids = posts.Select(post => post.Split("/")[^1].Split("#")[0]).ToList();
        var idChunks = ids.Chunk(100);
        var token = await TokenManager.Instance.GetOrGenerate(ParserName, GenerateRedgifsToken, ct: cancellationToken);
        RequestHeaders["Authorization"] = $"Bearer {token.Value}";
        foreach (var chunk in idChunks)
        {
            var idParam = string.Join("%2C", chunk);
            var request = RequestHeaders.ToRequest(HttpMethod.Get, $"{baseRequest}{idParam}");
            var response = await Client.SendAsync(request, cancellationToken);
            var responseJson =
                (await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: cancellationToken))!;
            var gifs = responseJson["gifs"]!.AsArray();
            images.AddRange(gifs.Select(gif => gif!["urls"]!["hd"]!.Deserialize<string>()).Select(gifUrl => gifUrl!)
                                .Select(dummy => (StringFileLinkWrapper)dummy));
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
    
    private static HttpClient GenerateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", Sdk.Configuration.Config.Instance.UserAgent);
        return client;
    }

    internal static async Task<Token> GenerateRedgifsToken(CancellationToken ct)
    {
        var response = await Client.GetAsync("https://api.redgifs.com/v2/auth/temporary", ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("Failed to get Redgifs token");
        }

        var json = (await response.Content.ReadFromJsonAsync<JsonNode>(ct))!;
        var value = json["token"].Deserialize<string>()
                    ?? throw new InvalidOperationException("Failed to get Redgifs token");
        return new Token(value, DateTime.Now + TimeSpan.FromHours(24));
    }
}