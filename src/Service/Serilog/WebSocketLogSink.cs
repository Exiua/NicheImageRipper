using System.Text.Json;
using NicheImageRipper.Service.Models.WebSocket;
using NicheImageRipper.Service.Singletons;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;

namespace NicheImageRipper.Service.Serilog;

/// <summary>Forwards log events to the WebSocket clients subscribed to their level.</summary>
public sealed class WebSocketLogSink(Func<WebSocketBroadcaster> broadcasterFactory) : ILogEventSink
{
    // The broadcaster's own events are left out: broadcasting them could feed back into more of them
    private static readonly ScalarValue BroadcasterSourceContext = new(typeof(WebSocketBroadcaster).FullName);

    private WebSocketBroadcaster? _broadcaster;

    public void Emit(LogEvent logEvent)
    {
        var broadcaster = _broadcaster ??= broadcasterFactory();
        if (!broadcaster.WantsLog(logEvent.Level) || IsFromBroadcaster(logEvent))
        {
            return;
        }

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

        broadcaster.BroadcastLog(logEvent.Level, new WsEnvelope
        {
            EventType = WsEventType.Log,
            Payload = JsonSerializer.SerializeToElement(payload),
        });
    }

    private static bool IsFromBroadcaster(LogEvent logEvent)
    {
        return logEvent.Properties.TryGetValue(Constants.SourceContextPropertyName, out var sourceContext) &&
               sourceContext.Equals(BroadcasterSourceContext);
    }
}

public static class WebSocketLogSinkExtensions
{
    public static LoggerConfiguration WebSocketLogs(
        this LoggerSinkConfiguration sinkConfiguration,
        Func<WebSocketBroadcaster> broadcasterFactory)
    {
        return sinkConfiguration.Sink(new WebSocketLogSink(broadcasterFactory));
    }
}
