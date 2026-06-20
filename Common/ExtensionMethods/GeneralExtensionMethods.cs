using System.Net;

namespace NicheImageRipper.Common.ExtensionMethods;

public static class GeneralExtensionMethods
{
    public static HttpRequestMessage ToRequest(this Dictionary<string, string> headers, HttpMethod method, string url, Dictionary<string, string>? parameters = null, HttpContent? content = null)
    {
        if (parameters is not null)
        {
            var query = string.Join("&", parameters.Select(x => $"{WebUtility.UrlEncode(x.Key)}={WebUtility.UrlEncode(x.Value)}"));
            url = $"{url}?{query}";
        }
        
        var request = new HttpRequestMessage(method, url);
        if (content is not null)
        {
            request.Content = content;
        }
        
        foreach (var (key, value) in headers)
        {
            request.Headers.TryAddWithoutValidation(key, value);
        }
        
        return request;
    }
    
    public static void Update<TKey, TValue>(this Dictionary<TKey, TValue> dst, Dictionary<TKey, TValue> src) where TKey : notnull
    {
        foreach (var (key, value) in src)
        {
            dst[key] = value;
        }
    }
}