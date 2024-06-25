using System.Web;

namespace Core.ExtensionMethods;

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
    
    public static IEnumerable<TResult> SelectWhere<TSource, TResult>(this IEnumerable<TSource> enumerable,
        Func<TSource, TResult> selectPredicate, Func<TResult, bool> wherePredicate)
    {
        return enumerable.Select(selectPredicate).Where(wherePredicate);
    }
    
    public static bool AnyIn(this IEnumerable<string> enumerable, string other)
    {
        return enumerable.Any(other.Contains);
    }

    public static List<T> RemoveDuplicates<T>(this IEnumerable<T> src)
    {
        return src.Distinct().ToList();
    }
    
    public static List<StringImageLinkWrapper> ToWrapperList(this List<string> src)
    {
        return src.Select(url => new StringImageLinkWrapper(url)).ToList();
    }

    public static IEnumerable<List<T>> Chunk<T>(this List<T> list, int size)
    {
        for(var i = 0; i < list.Count; i += size)
        {
            yield return list.GetRange(i, Math.Min(size, list.Count - i));
        }
    }

    public static string Join(this string separator, IEnumerable<string> values)
    {
        return string.Join(separator, values);
    }
    
    public static string Join(this IEnumerable<string> values, string separator)
    {
        return string.Join(separator, values);
    }
    
    public static string Remove(this string src, string toRemove)
    {
        return src.Replace(toRemove, string.Empty);
    }
}