using Serilog;
using Serilog.Events;

namespace Core.Utility;

public static class PrintUtility
{
    public static Action<string, LogEventLevel> PrintFunction { get; set; } = (msg, lvl) => Log.Write(lvl, msg);
    
    public static void Print(object? obj, LogEventLevel level = LogEventLevel.Information)
    {
        PrintFunction(obj?.ToString() ?? "null", level);
    }
    
    public static void Print(string message, LogEventLevel level = LogEventLevel.Information)
    {
        PrintFunction(message, level);
    }
}