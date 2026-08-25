using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using NicheImageRipper.Core.Exceptions;

using Sdk.DataStructures;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.Booru;

public abstract partial class BooruParser : HtmlParser
{
    protected BooruParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders,
                          FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager,
        requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Make requests to booru-like sites and extract image links
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected async Task<RipInfo> BooruParse(Core.Enums.Booru site, string? tags = null,
                                             CancellationToken cancellationToken = default)
    {
        if (tags is null)
        {
            Logger.Debug("Parsing {SiteName}", site);
        }
        else
        {
            Logger.Debug("Parsing {SiteName} with tags: {Tags}", site, tags);
        }

        var metadata = site.GetMetadata();
        var siteName = metadata.SiteName;
        var baseUrl = metadata.GetFullBaseUrl();
        var pageParameterName = metadata.PageParameterName;
        var startingPageIndex = metadata.StartingPageIndex;
        var limit = metadata.Limit;
        var headers = metadata.Headers;
        var jsonObjectNavigationToArray = metadata.JsonObjectNavigationToArray;
        var arrayMayNotExist = metadata.ArrayMayNotExist;
        var jsonObjectNavigationToUrl = metadata.JsonObjectNavigationToUrl;
        var delay = metadata.Delay;
        tags ??= ExtractTagsFromUrl(CurrentUrl);
        tags = Uri.UnescapeDataString(tags);
        var dirName = $"[{siteName}] " + tags.Remove("+").Remove("tags=");
        var session = new HttpClient();
        if (headers is not null)
        {
            foreach (var (key, value) in headers)
            {
                session.DefaultRequestHeaders.Add(key, value);
            }
        }

        var querySeparator = baseUrl[^1] == '&' || baseUrl[^1] == '?' ? "" : "&";

        var requestUrl = $"{baseUrl}{querySeparator}limit={limit}&{pageParameterName}={startingPageIndex}&{tags}";
        Logger.Debug("Request URL: {RequestUrl}", requestUrl);
        var response = await session.GetAsync(requestUrl, cancellationToken);
        JsonNode? json;
        if (!response.IsSuccessStatusCode)
        {
            var solution = await FlareSolverrManager.GetSiteSolution(requestUrl, cancellationToken: cancellationToken);
            var rawJson = solution.Response;
            var start = rawJson.IndexOf('[');
            var end = rawJson.LastIndexOf(']');
            rawJson = rawJson[start..(end + 1)];
            json = JsonSerializer.Deserialize<JsonNode>(rawJson);
        }
        else
        {
            try
            {
                json = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: cancellationToken);
            }
            catch (JsonException e) when (e.Message.StartsWith("The input does not contain any JSON tokens."))
            {
                Logger.Debug("Failed to deserialize json due to empty response");
                return RipInfo.Empty;
            }
        }

        Logger.Debug("Got Response");
        if (json is null)
        {
            throw new RipperException("Failed to deserialize json");
        }

        if (jsonObjectNavigationToArray is not null)
        {
            json = GetUrlArray(json, jsonObjectNavigationToArray, arrayMayNotExist);
        }

        Logger.Debug("Got Json Array");
        var data = json.AsArray();
        //Logger.Debug("Data: {@Data}", data);
        var images = new List<StringFileLinkWrapper>();
        var pid = startingPageIndex + 1;
        while (true)
        {
            // Extract URLs from the last page
            Logger.Debug("Parsing page {PageNumber}", pid);
            var urls = data.Select(post => GetUrl(post!, jsonObjectNavigationToUrl))
                           .OfType<string>()
                           .ToStringFileLinks();
            images.AddRange(urls);
            if (data.Count < limit)
            {
                break;
            }

            // Fetch the next page
            var pageUrl = $"{baseUrl}{querySeparator}limit={limit}&{pageParameterName}={pid}&{tags}";
            Logger.Debug("Fetching next page: {PageUrl}", pageUrl);
            response = await session.GetAsync(pageUrl, cancellationToken);
            #if DEBUG
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
            #endif
            json = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: cancellationToken);
            if (jsonObjectNavigationToArray is not null)
            {
                json = GetUrlArray(json!, jsonObjectNavigationToArray, arrayMayNotExist);
            }

            data = json!.AsArray();
            pid++;

            if (delay > 0)
            {
                await Task.Delay(delay, cancellationToken);
            }
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }

    private static string? GetUrl(JsonNode json, string[] jsonNavigation)
    {
        json = jsonNavigation.Aggregate(json, (current, nav) => current[nav]!);
        var url = json.Deserialize<string>();

        return url;
    }

    private static JsonNode GetUrlArray(JsonNode json, string[] jsonNavigation, bool arrayMayNotExist)
    {
        foreach (var name in jsonNavigation)
        {
            if (json[name] is null)
            {
                if (arrayMayNotExist)
                {
                    return new JsonArray();
                }

                throw new RipperException($"Failed to find json object: {name}");
            }

            json = json[name]!;
        }

        return json;
    }

    /// <summary>
    ///     Extracts tags from a booru-like URL. Tags will have the format "tags=tag1+tag2+tag3"
    /// </summary>
    /// <param name="url">The URL to extract tags from</param>
    /// <returns>A string containing the tags</returns>
    public static string ExtractTagsFromUrl(string url)
    {
        var tags = BooruRegex().Match(url).Groups[1].Value;
        return tags.IsNullOrEmpty() ? throw new RipperException("Failed to extract tags from URL") : tags;
    }

    [GeneratedRegex("(tags=[^&]+)")]
    private static partial Regex BooruRegex();
}