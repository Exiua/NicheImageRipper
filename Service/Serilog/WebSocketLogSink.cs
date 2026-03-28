using System.Text.Json;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using Service.Singletons;

namespace Service.Serilog;

public sealed class WebSocketLogSink : ILogEventSink
{
    private readonly WebSocketLogBroadcaster _broadcaster;

    public WebSocketLogSink(WebSocketLogBroadcaster broadcaster)
    {
        _broadcaster = broadcaster;
    }

    public void Emit(LogEvent logEvent)
    {
        var payload = new
        {
            timestamp = logEvent.Timestamp,
            level = logEvent.Level.ToString(),
            message = logEvent.RenderMessage(),
            exception = logEvent.Exception?.ToString(),
            properties = logEvent.Properties.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToString())
        };

        var json = JsonSerializer.Serialize(payload);

        _ = Task.Run(async () =>
        {
            try
            {
                await _broadcaster.BroadcastAsync(json);
            }
            catch(Exception e)
            {
                _broadcaster.Logger.LogError(e, "Error broadcasting log event");
            }
        });
    }
}

public static class WebSocketLogSinkExtensions
{
    public static LoggerConfiguration WebSocketLogs(
        this LoggerSinkConfiguration sinkConfiguration,
        WebSocketLogBroadcaster broadcaster)
    {
        return sinkConfiguration.Sink(new WebSocketLogSink(broadcaster));
    }
}