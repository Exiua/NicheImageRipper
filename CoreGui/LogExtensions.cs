using Avalonia;
using Avalonia.Logging;
using System.Collections.Generic;

namespace CoreGui;

public static class LogExtensions
{
    public static AppBuilder LogToSerilog(
        this AppBuilder builder,
        LogEventLevel level = LogEventLevel.Warning,
        params string[] areas)
    {
        Logger.Sink = new SerilogSink(level, areas);
        return builder;
    }
}

public class SerilogSink : ILogSink
{
    private readonly LogEventLevel _level;
    private readonly IList<string>? _areas;

    public SerilogSink(
        LogEventLevel minimumLevel,
        IList<string>? areas = null)
    {
        _level = minimumLevel;
        _areas = areas?.Count > 0 ? areas : null;
    }

    public bool IsEnabled(LogEventLevel level, string area)
    {
        return level >= _level && (_areas?.Contains(area) ?? true);
    }

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate)
    {
        if (IsEnabled(level, area))
        {
            Serilog.Log.Write(LogLevelToSerilogLevel(level), $"[{area} {source}] {messageTemplate}");
        }
    }

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate,
                    params object?[] propertyValues)
    {
        if (IsEnabled(level, area))
        {
            Serilog.Log.Write(LogLevelToSerilogLevel(level), $"[{area} {source}] {messageTemplate}", propertyValues);
        }
    }

    private static Serilog.Events.LogEventLevel LogLevelToSerilogLevel(LogEventLevel level)
    {
        switch (level)
        {
            case LogEventLevel.Verbose:
                return Serilog.Events.LogEventLevel.Verbose;
            case LogEventLevel.Debug:
                return Serilog.Events.LogEventLevel.Debug;
            case LogEventLevel.Information:
                return Serilog.Events.LogEventLevel.Information;
            case LogEventLevel.Warning:
                return Serilog.Events.LogEventLevel.Warning;
            case LogEventLevel.Error:
                return Serilog.Events.LogEventLevel.Error;
            case LogEventLevel.Fatal:
                return Serilog.Events.LogEventLevel.Fatal;
            default:
                return Serilog.Events.LogEventLevel.Verbose;
        }
    }
}