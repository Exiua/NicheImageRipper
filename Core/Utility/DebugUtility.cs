using System.Diagnostics;

namespace Core.Utility;

public static class DebugUtility
{
    [Conditional("DEBUG")]
    public static void Print(string? value)
    {
        Console.WriteLine(value);   
    }
}