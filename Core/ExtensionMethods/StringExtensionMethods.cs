using System.Web;
using Core.SiteParsing;

namespace Core.ExtensionMethods;

public static class StringExtensionMethods
{
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

    public static string Join(this string separator, IEnumerable<string> values)
    {
        return string.Join(separator, values);
    }

    public static List<StringImageLinkWrapper> IntoStringImageLinkWrapperList(this string src)
    {
        return [new StringImageLinkWrapper(src)];
    }

    public static string Remove(this string src, string toRemove)
    {
        return src.Replace(toRemove, string.Empty);
    }

    public static bool IsNullOrEmpty(this string? s)
    {
        return string.IsNullOrEmpty(s);
    }

    public static string JoinWith(this IEnumerable<string> values, string separator)
    {
        return string.Join(separator, values);
    }

    public static int ToInt(this string s)
    {
        return int.Parse(s);
    }

    /// <summary>
    ///     Remove HTML artifacts from a url string (such as &amp;) and replace them with their proper characters
    /// </summary>
    /// <param name="s">URL string to clean</param>
    /// <returns>Cleaned URL string</returns>
    public static string DecodeUrl(this string s)
    {
        return HttpUtility.HtmlDecode(s);
    }
    
    public static bool Contains(this string s, params string[] substrings)
    {
        return substrings.All(s.Contains);
    }

    public static string TrimStartMatches(this string input, string prefix)
    {
        return input.StartsWith(prefix) ? input[prefix.Length..] : input;
    }
}