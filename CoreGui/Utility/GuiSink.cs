using System;
using CoreGui.Views;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;

namespace CoreGui.Utility;

public class GuiSink(IFormatProvider? formatProvider) : ILogEventSink
{
    public static event Action<string>? OnLog;
    public static MainWindow? MainWindow { get; set; }

    public void Emit(LogEvent logEvent)
    {
        if (logEvent.Level < LogEventLevel.Information)
        {
            return;
        }
        
        var message = logEvent.RenderMessage(formatProvider);
        OnLog?.Invoke(message);
        MainWindow?.OnLog(message);
    }
}

public static class GuiSinkExtensions
{
    public static LoggerConfiguration Gui(
        this LoggerSinkConfiguration loggerConfiguration,
        IFormatProvider? formatProvider = null)
    {
        return loggerConfiguration.Sink(new GuiSink(formatProvider));
    }
}