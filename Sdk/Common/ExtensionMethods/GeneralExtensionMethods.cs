using System.Net;
using System.Runtime.CompilerServices;
using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.SiteParsing;

namespace NicheImageRipper.Sdk.Common.ExtensionMethods;

public static class GeneralExtensionMethods
{
    public static IEnumerable<StringFileLinkWrapper> ToStringFileLinks(this IEnumerable<string> src)
    {
        return src.Select(url => new StringFileLinkWrapper(url));
    }
    
    public static IEnumerable<StringFileLinkWrapper> ToStringFileLinks(this IEnumerable<FileLink> src)
    {
        return src.Select(url => new StringFileLinkWrapper(url));
    }
    
    public static List<StringFileLinkWrapper> ToStringFileLinkWrapperList(this IEnumerable<string> src)
    {
        return src.Select(url => new StringFileLinkWrapper(url)).ToList();
    }
    
    public static List<StringFileLinkWrapper> ToStringFileLinkWrapperList(this IEnumerable<FileLink> src)
    {
        return src.Select(url => new StringFileLinkWrapper(url)).ToList();
    }
    
    public static IEnumerable<(int i, T)> Enumerate<T>(this T[] list, int start = 0)
    {
        for (var i = start; i < list.Length; i++)
        {
            yield return (i, list[i]);
        }
    }
    
    public static IEnumerable<(int i, T)> Enumerate<T>(this List<T> list)
    {
        return list.Select((t, i) => (i, t));
    }
    
    public static IEnumerable<(int i, T)> Enumerate<T>(this IEnumerable<T> enumerable, int start = 0)
    {
        var i = start;
        foreach (var item in enumerable)
        {
            yield return (i++, item);
        }
    }

    public static ConfiguredCancelableAsyncEnumerable<(int, T item)> EnumerateAsync<T>(this IAsyncEnumerable<T> enumerable, int start = 0, CancellationToken cancellationToken = default)
    {
        var i = start;
        return enumerable.Select(item => (i++, item)).WithCancellation(cancellationToken);
    }
    
    public static List<T> RemoveDuplicates<T>(this IEnumerable<T> src)
    {
        return src.Distinct().ToList();
    }

    public static HttpRequestMessage ToRequest(this Dictionary<string, string> headers, HttpMethod method, string url,
                                               Dictionary<string, string>? parameters = null,
                                               HttpContent? content = null)
    {
        if (parameters is not null)
        {
            var query = string.Join("&",
                parameters.Select(x => $"{WebUtility.UrlEncode(x.Key)}={WebUtility.UrlEncode(x.Value)}"));
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

    public static void Update<TKey, TValue>(this Dictionary<TKey, TValue> dst, Dictionary<TKey, TValue> src)
        where TKey : notnull
    {
        foreach (var (key, value) in src)
        {
            dst[key] = value;
        }
    }
    
    public static string Join(this IEnumerable<string> values, char separator)
    {
        return string.Join(separator, values);
    }
}