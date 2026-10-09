using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using ImpersonateClient;
using PatreonApiClient.Models;
using PatreonApiClient.Models.Exceptions;
using Serilog;

namespace PatreonApiClient;

public partial class PatreonClient : IDisposable
{
    private readonly HttpClient _httpClient;

    private bool _disposed;

    public PatreonClient(string sessionId)
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Cookie", $"session_id={sessionId}");
    }

    public async IAsyncEnumerable<PatreonPostsResponse> GetPosts(string creatorUrl,
                                                                 [EnumeratorCancellation]
                                                                 CancellationToken cancellationToken = default)
    {
        var id = await GetCreatorId(creatorUrl, cancellationToken);
        if (id == string.Empty)
        {
            Log.Error("Failed to get creator ID. Try changing your IP or using a VPN.");
            throw new PatreonClientException("Failed to get creator ID. Try changing your IP or using a VPN.");
        }

        Log.Debug("Retrieved creator ID: {CreatorId}", id);
        var queryParameters = new Dictionary<string, string>
        {
            ["include"] =
                "access_rules,access_rules.tier.null,campaign,custom_thumbnail_media.null,images,livestream,livestream.display,livestream.state,media,primary_image,rss_synced_feed,user,user_defined_tags,video.null",
            ["fields[campaign]"] = "avatar_photo_url,name,url",
            ["fields[post]"] =
                "attachments_preview_metadata,content_json_string,content_teaser_text,created_at,edited_at,embed,image,patreon_url,post_file,post_type,title,url,video",
            ["fields[post_tag]"] = "tag_type,value",
            ["fields[user]"] = "image_url,full_name,url",
            ["fields[access_rule]"] = "access_rule_type,amount_cents",
            ["fields[livestream]"] = "display,state",
            ["fields[media]"] = "id,image_urls,display,download_url,metadata,file_name,state",
            ["filter[campaign_id]"] = id,
            ["filter[contains_exclusive_posts]"] = "true",
            ["filter[include_lives]"] = "true",
            ["filter[include_drops]"] = "true",
            ["sort"] = "-published_at",
            ["json-api-use-default-includes"] = "true",
            ["json-api-version"] = "1.0",
        };

        const string baseUrl = "https://www.patreon.com/api/posts";
        string? cursor = null;
        while (true)
        {
            if (cursor is not null)
            {
                queryParameters["page[cursor]"] = cursor;
            }

            var requestUrl = baseUrl + "?" + string.Join('&',
                queryParameters.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
            var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
            var content = await response.Content.ReadFromJsonAsync<PatreonPostsResponse>(cancellationToken);
            if (content is null)
            {
                Log.Error("Failed to get posts. Response content is null.");
                throw new PatreonClientException("Failed to get posts. Response content is null.");
            }

            yield return content;

            cursor = content.Meta?.Pagination?.Cursors?.Next;
            if (cursor is null)
            {
                yield break;
            }
        }
    }

    private static async Task<string> GetCreatorId(string creatorUrl, CancellationToken cancellationToken = default)
    {
        var client = ImpersonateHttpClient.Builder()
                                          .WithBrowser("chrome".IntoImpersonateTarget())
                                          .Build();
        var response = await client.GetAsync(creatorUrl, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        //Console.WriteLine(content);
        var match = CreatorIdRegex().Match(content);
        return !match.Success ? string.Empty : match.Groups[1].Value;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }

    [GeneratedRegex("/p/campaign/(\\d+)/")]
    private static partial Regex CreatorIdRegex();
}