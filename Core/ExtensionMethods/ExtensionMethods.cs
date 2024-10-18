using System.Runtime.CompilerServices;
using System.Web;
using Core.DataStructures;
using Core.SiteParsing;
using OpenQA.Selenium;

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

    public static IEnumerable<(int i, char)> Enumerate(this string s, int start = 0)
    {
        for(var i = start; i < s.Length; i++)
        {
            yield return (i, s[i]);
        }
    }

    public static string ToTitle(this string src)
    {
        return src[0].ToString().ToUpper() + src[1..];
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

    public static int ToInt(this string s)
    {
        return int.Parse(s);
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
    
    public static void AddNotNull<T>(this List<T> list, T? item)
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

    public static Cookie ToSeleniumCookie(this FlareSolverrIntegration.Responses.Cookie cookie)
    {
        var expiration = DateTimeOffset.FromUnixTimeSeconds(cookie.Expiry).UtcDateTime;
        var seleniumCookie = new Cookie(cookie.Name, cookie.Value, cookie.Domain, cookie.Path, expiration, cookie.Secure, 
            cookie.HttpOnly, cookie.SameSite);
        return seleniumCookie;
    }
}