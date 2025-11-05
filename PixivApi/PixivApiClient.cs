using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Common.ExtensionMethods;
using PixivApi.Exceptions;
using PixivApi.Models;

namespace PixivApi;

public class PixivApiClient
{
    private const string ClientId = "MOBrBDS8blbauoSck0ZfDbtuzpyT";
    private const string ClientSecret = "lsACyCD94FhDUtGTXi3QzcFE2uU1hqtDaKeqrdwj";
    private const string HashSecret = "28c1fdd170a5204386cb1313c7077b34f83e4aaf4aa829ce78c231e05b0bae2c";

    private string UserId { get; set; }
    private string? AccessToken { get; set; }
    private string? RefreshToken { get; set; }
    private string Hosts { get; set; } = "https://app-api.pixiv.net";
    private HttpClient Requests { get; set; } = new();
    private Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    
    public PixivApiClient(Dictionary<string, string>? headers = null)
    {
        if (headers is not null)
        {
            Headers = headers;
        }
    }

    /// <summary>
    ///     Manually specify additional headers. will overwrite API default headers in case of collision
    /// </summary>
    /// <param name="additionalHeaders"></param>
    public void SetAdditionalHeaders(Dictionary<string, string> additionalHeaders)
    {
        Headers = additionalHeaders;
    }

    /// <summary>
    ///     Set header Accept-Language for all requests (useful for get tags.translated_name)
    /// </summary>
    /// <param name="language"></param>
    public void SetAcceptLanguage(string language)
    {
        Headers["Accept-Language"] = language;
    }

    public void RequireAuth()
    {
        if (AccessToken is null)
        {
            const string msg = "Authentication required! Call Login() or SetAuth() first!";
            throw new PixivApiException(msg);
        }
    }

    public async Task<HttpResponseMessage> RequestsCall(HttpMethod method,
                                                        string url,
                                                        Dictionary<string, string>? headers = null,
                                                        Dictionary<string, string>? parameters = null,
                                                        HttpContent? data = null,
                                                        bool stream = false)
    {
        // Make a copy of the dictionary
        var mergedHeaders = Headers.ToDictionary();
        if (headers is not null)
        {
            mergedHeaders.Update(headers);
        }

        var completionOption = stream ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead;
        try
        {
            switch (method)
            {
                case HttpMethod.Get:
                {
                    var request = mergedHeaders.ToRequest(method.ToHttpMethod(), url, parameters: parameters);
                    return await Requests.SendAsync(request, completionOption);
                }
                case HttpMethod.Post:
                case HttpMethod.Delete:
                {
                    var request = mergedHeaders.ToRequest(method.ToHttpMethod(), url, parameters: parameters,
                        content: data);
                    return await Requests.SendAsync(request, completionOption);
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(method), method, null);
            }
        }
        catch (Exception e)
        {
            var msg = $"requests {method} {url}";
            throw new PixivApiException(msg, e);
        }
    }

    public void SetAuth(string accessToken, string? refreshToken = null)
    {
        AccessToken = accessToken;
        RefreshToken = refreshToken;
    }

    public Task<AuthResponse> Login(string username, string password)
    {
        return Auth(username, password);
    }

    public void SetClient(string clientId, string clientSecret)
    {
        UserId = clientId;
        AccessToken = clientSecret;
    }

    private static StringContent ToJsonContent<T>(T obj)
    {
        var json = JsonSerializer.Serialize(obj);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private static FormUrlEncodedContent ToFormUrlEncodedContent(Dictionary<string, string> payload)
    {
        return new FormUrlEncodedContent(payload);
    }

    public Task<AuthResponse> Auth(string username, string password, Dictionary<string, string>? headers = null)
    {
        var data = new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = username,
            ["password"] = password,
        };
        
        return Auth(data, headers);
    }

    public Task<AuthResponse> Auth(string? refreshToken = null, Dictionary<string, string>? headers = null)
    {
        var token = refreshToken ?? AccessToken;
        if (token is null)
        {
            throw new PixivApiException("No refresh token provided!");
        }

        var data = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = token,
        };
        
        return Auth(data, headers);
    }
    
    private async Task<AuthResponse> Auth(Dictionary<string, string> data, Dictionary<string, string>? headers = null)
    {
        var localTime = DateTime.Now.ToString("yyyy-MM-dd'T'HH:mm:ss+00:00");
        var _headers = headers ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _headers["x-client-time"] = localTime;
        var input = localTime + HashSecret;
        var inputBytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = MD5.HashData(inputBytes);
        var clientHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        _headers["x-client-hash"] = clientHash;
        if (!_headers.ContainsKey("user-agent"))
        {
            _headers["app-os"] = "ios";
            _headers["app-os-version"] = "14.6";
            _headers["user-agent"] = "PixivIOSApp/7.13.3 (iOS 14.6; iPhone13,2)";
        }

        string authHosts;
        if (Hosts == "https://app-api.pixiv.net")
        {
            authHosts = "https://oauth.secure.pixiv.net";
        }
        else
        {
            authHosts = Hosts;
            _headers["host"] = "oauth.secure.pixiv.net";
        }

        var url = $"{authHosts}/auth/token";
        data["get_secure_url"] = "1";
        data["client_id"] = ClientId;
        data["client_secret"] = ClientSecret;

        var request = _headers.ToRequest(System.Net.Http.HttpMethod.Post, url, content: ToFormUrlEncodedContent(data));
        var response = await Requests.SendAsync(request);
        var statusCode = response.StatusCode;
        // If statusCode is not 200, 301, or 302
        if (statusCode != HttpStatusCode.OK && statusCode != HttpStatusCode.MovedPermanently &&
            statusCode != HttpStatusCode.Found)
        {
            var text = await response.Content.ReadAsStringAsync();
            string msg;
            if (data["grant_type"] == "password")
            {
                msg = $"[ERROR] auth() failed! check username and password.\nHTTP {statusCode}: {text}";
                throw new PixivRequestException(msg, _headers, text);
            }

            msg = $"[ERROR] auth() failed! check refresh_token.\nHTTP {statusCode}: {text}";
            throw new PixivRequestException(msg, _headers, text);
        }

        string? rawToken = null;
        try
        {
            rawToken = await response.Content.ReadAsStringAsync();
            var token = JsonSerializer.Deserialize<AuthResponse>(rawToken);
            if (token is null)
            {
                var msg = $"Failed to deserialize access_token. Response: {rawToken}";
                throw new PixivRequestException(msg, _headers, rawToken);
            }
            
            UserId = token.User.Id;
            AccessToken = token.AccessToken;
            RefreshToken = token.RefreshToken;

            return token;
        }
        catch (Exception e)
        {
            var msg = $"Failed to deserialize access_token. Response: {rawToken}";
            throw  new PixivRequestException(msg, e, _headers, rawToken ?? "null");
        }
    }

    public async Task<bool> Download(string url,
                               string prefix = "",
                               string? path = null,
                               string? name = null,
                               bool replace = false,
                               string referer = "https://app-api.pixiv.net/")
    {
        path ??= Directory.GetCurrentDirectory();
        var filename = prefix + (name ?? Path.GetFileName((new Uri(url)).LocalPath));
        var filepath = Path.Combine(path, filename);
        if (File.Exists(filepath) && !replace)
        {
            return false;
        }

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Referer"] = referer,
        };
        
        var response = await RequestsCall(HttpMethod.Get, url, headers: headers, stream: true);
        await using var destStream = File.Create(filepath);
        await using var srcStream = await response.Content.ReadAsStreamAsync();
        await srcStream.CopyToAsync(destStream);

        return true;
    }
    
    // SetApiProxy
    
    public async Task<HttpResponseMessage> NoAuthRequestsCall(HttpMethod method,
                                                        string url,
                                                        Dictionary<string, string>? headers = null,
                                                        Dictionary<string, string>? parameters = null,
                                                        HttpContent? data = null,
                                                        bool reqAuth = true)
    {
        var _headers = headers ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (Hosts != "https://app-api.pixiv.net")
        {
            _headers["host"] = "app-api.pixiv.net";
        }
        
        if (!_headers.ContainsKey("user-agent"))
        {
            _headers["app-os"] = "ios";
            _headers["app-os-version"] = "14.6";
            _headers["user-agent"] = "PixivIOSApp/7.13.3 (iOS 14.6; iPhone13,2)";
        }

        if (!reqAuth)
        {
            return await RequestsCall(method, url, headers: _headers, parameters: parameters, data: data);
        }
        
        RequireAuth();
        _headers["Authorization"] = $"Bearer {AccessToken}";
        return await RequestsCall(method, url, headers: _headers, parameters: parameters, data: data);
    }

    #region API Endpoints

    // user_detail

    public async Task<UserIllustrations> UserIllusts(string userId,
                                                     RequestType type = RequestType.Illust,
                                                     RequestFilter filter = RequestFilter.ForIos,
                                                     int offset = 0,
                                                     bool reqAuth = true)
    {
        var url = $"{Hosts}/v1/user/illusts";
        var parameters = new Dictionary<string, string>
        {
            ["user_id"] = userId,
            ["filter"] = filter.ToRequestFilterString(),
            ["type"] = type.ToRequestTypeString(),
        };
        
        if (offset > 0)
        {
            parameters["offset"] = offset.ToString();
        }
        
        var response = await NoAuthRequestsCall(HttpMethod.Get, url, parameters: parameters, reqAuth: reqAuth);
        var userIllusts = await response.Content.ReadFromJsonAsync<UserIllustrations>();
        if (userIllusts is null)
        {
            throw new PixivApiException("Failed to deserialize UserIllustrations");
        }
        
        return userIllusts;
    }
    
    // user_bookmarks_illust
    
    // user_bookmarks_novel
    
    // user_related
    
    // user_recommended
    
    // illust_follow
    
    // illust_detail
    
    // illust_comments
    
    // illust_related
    
    // illust_recommended
    
    // novel_comments
    
    // novel_recommended
    
    // illust_ranking
    
    // trending_tags_illust
    
    // search_illust
    
    // search_novel
    
    // search_user
    
    // illust_bookmark_detail
    
    // illust_bookmark_add
    
    // illust_bookmark_delete
    
    // user_follow_add
    
    // user_follow_delete
    
    // user_edit_ai_show_settings
    
    // user_bookmark_tags_illust
    
    // user_following
    
    // user_follower
    
    // user_mypixiv
    
    // user_list
    
    public async Task<UgoiraMetadata> UgoiraMetadata(string illustId, bool reqAuth = true)
    {
        var url = $"{Hosts}/v1/ugoira/metadata";
        var parameters = new Dictionary<string, string>
        {
            ["illust_id"] = illustId,
        };
        
        var response = await NoAuthRequestsCall(HttpMethod.Get, url, parameters: parameters, reqAuth: reqAuth);
        var rawUgoiraMetadata = await response.Content.ReadAsStringAsync();
        rawUgoiraMetadata = rawUgoiraMetadata.Replace("\"body\":[]", "\"body\":null"); // workaround for deserialization issue
        var ugoiraMetadata = JsonSerializer.Deserialize<UgoiraMetadata>(rawUgoiraMetadata);
        if (ugoiraMetadata is null)
        {
            throw new PixivApiException("Failed to deserialize UgoiraMetadata");
        }
        
        return ugoiraMetadata;
    }
    
    // user_novels
    
    // novel_series
    
    // novel_detail
    
    // novel_new
    
    // novel_follow
    
    // webview_novel
    
    // novel_text
    
    // illust_new
    
    // showcase_article

    #endregion
}

public enum HttpMethod
{
    Get,
    Post,
    Delete,
}

public static class HttpMethodExtensionMethods
{
    public static System.Net.Http.HttpMethod ToHttpMethod(this HttpMethod method)
    {
        return method switch
        {
            HttpMethod.Get => System.Net.Http.HttpMethod.Get,
            HttpMethod.Post => System.Net.Http.HttpMethod.Post,
            HttpMethod.Delete => System.Net.Http.HttpMethod.Delete,
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, null)
        };
    }
}

public enum RequestType
{
    Illust,
    Manga,
    None
}

public enum RequestFilter
{
    ForIos,
    None
}

public static class RequestTypeExtensionMethods
{
    public static string ToRequestTypeString(this RequestType type)
    {
        return type switch
        {
            RequestType.Illust => "illust",
            RequestType.Manga => "manga",
            RequestType.None => "",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }
}

public static class RequestFilterExtensionMethods
{
    public static string ToRequestFilterString(this RequestFilter filter)
    {
        return filter switch
        {
            RequestFilter.ForIos => "for_ios",
            RequestFilter.None => "",
            _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, null)
        };
    }
}