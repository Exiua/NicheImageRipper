using System.Diagnostics.CodeAnalysis;

namespace Sdk.Common.ExtensionMethods;

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

    public static string Remove([NotNull] this string s, [NotNull] string toRemove)
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
}