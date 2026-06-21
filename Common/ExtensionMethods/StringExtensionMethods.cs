using System.Diagnostics.CodeAnalysis;

namespace NicheImageRipper.Common.ExtensionMethods;

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
}