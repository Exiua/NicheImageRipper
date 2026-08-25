using Sdk.DataStructures;
using Sdk.SiteParsing;

namespace Sdk.Common.ExtensionMethods;

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
    
    public static List<T> RemoveDuplicates<T>(this IEnumerable<T> src)
    {
        return src.Distinct().ToList();
    }
}