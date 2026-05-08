using System;
using Gui.Models;
using Gui.Views;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;

namespace Gui.Utility;

public sealed class GuiSink : ILogEventSink
{
    private readonly IFormatProvider? _formatProvider;

    public GuiSink(IFormatProvider? formatProvider = null)
    {
        _formatProvider = formatProvider;
    }

    public void Emit(LogEvent logEvent)
    {
        if (logEvent.Level < LogEventLevel.Information)
        {
            return;
        }

        var entry = new LogEntryModel
        {
            Timestamp = logEvent.Timestamp,
            Level = logEvent.Level.ToString(),
            Message = logEvent.RenderMessage(_formatProvider),
            MessageTemplate = logEvent.MessageTemplate.Text,
            Exception = logEvent.Exception?.ToString()
        };

        GuiLogBridge.Publish(entry);
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