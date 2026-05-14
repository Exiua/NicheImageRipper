using System.Diagnostics.CodeAnalysis;

namespace Common.ExtensionMethods;

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
}