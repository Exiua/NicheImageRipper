using System.Web;

namespace Core;

public static class ExtensionMethods
{
    public static IEnumerable<(int i, T)> Enumerate<T>(this T[] list, int start = 0)
    {
        for (var i = start; i < list.Length; i++)
        {
            yield return (i, list[i]);
        }
    }
    
    public static IEnumerable<(int i, T)> Enumerate<T>(this List<T> list, int start = 0)
    {
        for (var i = start; i < list.Count; i++)
        {
            yield return (i, list[i]);
        }
    }

    public static HttpRequestMessage ToRequest(this Dictionary<string, string> headers, HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        foreach (var (key, value) in headers)
        {
            request.Headers.TryAddWithoutValidation(key, value);
        }
        
        return request;
    }
    
    public static string ToQueryString(this string url, Dictionary<string, string> dict)
    {
        var uriBuilder = new UriBuilder(url);
        var query = HttpUtility.ParseQueryString(uriBuilder.Query);
        foreach (var (key, value) in dict)
        {
            query[key] = value;
        }
        uriBuilder.Query = query.ToString();
        return uriBuilder.ToString();
    }
    
    public static string HexDigest(this byte[] bytes)
    {
        return BitConverter.ToString(bytes).Replace("-", "").ToLower();
    }
}