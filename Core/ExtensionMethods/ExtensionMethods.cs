using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.SiteParsing;
using Sdk.DataStructures;
using Sdk.SiteParsing;

namespace NicheImageRipper.Core.ExtensionMethods;

public static class ExtensionMethods
{
    public static string HexDigest(this byte[] bytes)
    {
        return Convert.ToHexStringLower(bytes);
    }
    
    public static string Join(this IEnumerable<string> values, char separator)
    {
        return string.Join(separator, values);
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