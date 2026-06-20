using System.Diagnostics;

namespace NicheImageRipper.Core.Utility;

public static class DebugUtility
{
    [Conditional("DEBUG")]
    public static void Print(string? value)
    {
        Console.WriteLine(value);   
    }
    
    [Conditional("DEBUG")]
    public static void Pause()
    {
        Console.WriteLine("Press any key to continue...");
        Console.ReadKey();
    }
}