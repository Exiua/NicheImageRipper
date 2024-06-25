using Core.Utility;

namespace Core.ExtensionMethods;

public static class DebugExtensionMethods
{
    public static void Print<TKey, TValue>(this IDictionary<TKey, TValue> dictionary) where TKey : notnull
    {
        PrintUtility.Print("{");
        foreach (var (key, value) in dictionary)
        {
            PrintUtility.Print($"{key}: {value}");
        }

        PrintUtility.Print("}");
    }
    
    public static void Print<T>(this IEnumerable<T> enumerable)
    {
        PrintUtility.Print("[");
        foreach (var item in enumerable)
        {
            PrintUtility.Print(item);
        }

        PrintUtility.Print("]");
    }
}