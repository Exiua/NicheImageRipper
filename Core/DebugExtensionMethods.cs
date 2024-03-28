namespace Core;

public static class DebugExtensionMethods
{
    public static void Print<TKey, TValue>(this IDictionary<TKey, TValue> dictionary) where TKey : notnull
    {
        Console.WriteLine("{");
        foreach (var (key, value) in dictionary)
        {
            Console.WriteLine($"{key}: {value}");
        }

        Console.WriteLine("}");
    }
    
    public static void Print<T>(this IEnumerable<T> enumerable)
    {
        Console.WriteLine("[");
        foreach (var item in enumerable)
        {
            Console.WriteLine(item);
        }

        Console.WriteLine("]");
    }
}