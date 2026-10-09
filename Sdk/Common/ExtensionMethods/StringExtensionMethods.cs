using System.Diagnostics.CodeAnalysis;
using System.Web;
using NicheImageRipper.Sdk.SiteParsing;

namespace NicheImageRipper.Sdk.Common.ExtensionMethods;

public static class StringExtensionMethods
{
    public static bool IsNullOrEmpty([NotNullWhen(false)]this string? s)
    {
        return string.IsNullOrEmpty(s);
    }
    
    public static bool IsNullOrWhiteSpace([NotNullWhen(false)]this string? s)
    {
        return string.IsNullOrWhiteSpace(s);
    }

    public static string Remove(this string s, string toRemove)
    {
        return s.Replace(toRemove, string.Empty);
    }
    
    public static string Join(this string s, IEnumerable<string> values)
    {
        return string.Join(s, values);
    }
    
    public static string Join(this IEnumerable<string> values, string separator)
    {
        return string.Join(separator, values);
    }
        
    public static int ParseInt(this string s)
    {
        return int.Parse(s);
    }
    
    /// <summary>
    ///     Remove HTML artifacts from a url string (such as &amp;) and replace them with their proper characters
    /// </summary>
    /// <returns>Cleaned URL string</returns>
    public static string DecodeUrl(this string s)
    {
        return HttpUtility.HtmlDecode(s);
    }
        
    /// <summary>
    ///     Remove percent-encoding from a url string.
    /// </summary>
    /// <returns>Cleaned URL string</returns>
    public static string UnescapeUrl(this string s)
    {
        return Uri.UnescapeDataString(s);
    }

    public static string JoinWith(this IEnumerable<string> values, string separator)
    {
        return string.Join(separator, values);
    }
    
    
    public static IEnumerable<(int i, char)> Enumerate(this string s, int start = 0)
    {
        for(var i = start; i < s.Length; i++)
        {
            yield return (i, s[i]);
        }
    }

    public static string ToTitle(this string s)
    {
        return s[0].ToString().ToUpper() + s[1..];
    }

    public static List<StringFileLinkWrapper> IntoStringImageLinkWrapperList(this string s)
    {
        return [new StringFileLinkWrapper(s)];
    }
}