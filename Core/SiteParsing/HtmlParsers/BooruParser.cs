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

public abstract class BooruParser : HtmlParser
{
    protected BooruParser(WebDriver driver, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, filenameScheme)
    {
    }
    
    /// <summary>
    ///     Make requests to booru-like sites and extract image links
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected async Task<RipInfo> BooruParse(Booru site, string? tags = null)
    {
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
        tags ??= BooruRegex().Match(CurrentUrl).Groups[1].Value;
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
        Log.Debug("Request URL: {RequestUrl}", requestUrl);
        var response = await session.GetAsync(requestUrl);
        JsonNode? json;
        if (!response.IsSuccessStatusCode)
        {
            var solution = await FlareSolverrManager.GetSiteSolution(requestUrl);
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
                json = await response.Content.ReadFromJsonAsync<JsonNode>();
            }
            catch (JsonException e) when (e.Message.StartsWith("The input does not contain any JSON tokens."))
            {
                Log.Debug("Failed to deserialize json due to empty response");
                return RipInfo.Empty;
            }
        }

        Log.Debug("Got Response");
        if (json is null)
        {
            throw new RipperException("Failed to deserialize json");
        }

        if (jsonObjectNavigationToArray is not null)
        {
            json = GetUrlArray(json, jsonObjectNavigationToArray, arrayMayNotExist);
        }

        Log.Debug("Got Json Array");
        var data = json.AsArray();
        //Log.Debug("Data: {@Data}", data);
        var images = new List<StringImageLinkWrapper>();
        var pid = startingPageIndex + 1;
        while (true)
        {
            Log.Debug("Fetching page {PageNumber}", pid);
            var urls = data.Select(post => GetUrl(post!, jsonObjectNavigationToUrl))
                           .OfType<string>()
                           .ToStringImageLinks();
            images.AddRange(urls);
            if (data.Count < limit)
            {
                break;
            }

            response = await session.GetAsync(
                $"{baseUrl}{querySeparator}limit={limit}&{pageParameterName}={pid}&{tags}");
            json = await response.Content.ReadFromJsonAsync<JsonNode>();
            if (jsonObjectNavigationToArray is not null)
            {
                json = GetUrlArray(json!, jsonObjectNavigationToArray, arrayMayNotExist);
            }

            data = json!.AsArray();
            pid++;
            
            if (delay > 0)
            {
                await Task.Delay(delay);
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
}