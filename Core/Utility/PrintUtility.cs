namespace Core.Utility;

public static class PrintUtility
{
    public static Action<string> PrintFunction { get; set; } = Console.WriteLine;
    
    public static void Print(object? obj)
    {
        PrintFunction(obj?.ToString() ?? "null");
    }
    
    public static void Print(string message)
    {
        PrintFunction(message);
    }
}