using Core.DataStructures;
using Core.SiteParsing;

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

    public static HttpRequestMessage ToRequest(this Dictionary<string, string> headers, HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        foreach (var (key, value) in headers)
        {
            request.Headers.TryAddWithoutValidation(key, value);
        }
        
        return request;
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

    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> src) where T : class
    {
        return src.OfType<T>();
    }
    
    public static List<T> RemoveDuplicates<T>(this IEnumerable<T> src)
    {
        return src.Distinct().ToList();
    }

    public static IEnumerable<List<T>> Chunk<T>(this List<T> list, int size)
    {
        for(var i = 0; i < list.Count; i += size)
        {
            yield return list.GetRange(i, Math.Min(size, list.Count - i));
        }
    }
    
    public static string Join(this IEnumerable<string> values, string separator)
    {
        return string.Join(separator, values);
    }
    
    public static string Join(this IEnumerable<string> values, char separator)
    {
        return string.Join(separator, values);
    }

    public static IEnumerable<StringImageLinkWrapper> ToStringImageLinks(this IEnumerable<string> src)
    {
        return src.Select(url => new StringImageLinkWrapper(url));
    }
    
    public static IEnumerable<StringImageLinkWrapper> ToStringImageLinks(this IEnumerable<ImageLink> src)
    {
        return src.Select(url => new StringImageLinkWrapper(url));
    }
    
    public static List<StringImageLinkWrapper> ToStringImageLinkWrapperList(this IEnumerable<string> src)
    {
        return src.Select(url => new StringImageLinkWrapper(url)).ToList();
    }

    public static T[] Pop<T>(this T[] src, int index)
    {
        if (src.Length <= 1)
        {
            return [];
        }
        var newArr = new T[src.Length - 1];
        Array.Copy(src, 0, newArr, 0, index);
        Array.Copy(src, index + 1, newArr, index, src.Length - index - 1);
        return newArr;
    }
    
    public static void AddIfNotNull<T>(this List<T> list, T? item)
    {
        if (item is not null)
        {
            list.Add(item);
        }
    }
    
    public static string ToSqliteString(this DateTime dateTime)
    {
        return dateTime.ToString("yyyy-MM-dd HH:mm:ss");
    }
    
    public static IEnumerable<Memory<T>> Chunks<T>(this Memory<T> source, int chunkSize)
    {
        for (var i = 0; i < source.Length; i += chunkSize)
        {
            yield return source.Slice(i, Math.Min(chunkSize, source.Length - i));
        }
    }

    public static int IndexOf<T>(this Memory<T> source, T val)
    {
        var span = source.Span;
        for (var i = 0; i < span.Length; i++)
        {
            if (span[i]?.Equals(val) == true)
            {
                return i;
            }
        }
        
        return -1;
    }
}