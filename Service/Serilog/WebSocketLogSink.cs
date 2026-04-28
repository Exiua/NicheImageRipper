using System.Text.Json;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using Service.Models.WebSocket;
using Service.Singletons;

namespace Service.Serilog;

public sealed class WebSocketLogSink : ILogEventSink
{
    private readonly Func<WebSocketBroadcaster> _broadcasterFactory;

    public WebSocketLogSink(Func<WebSocketBroadcaster> broadcasterFactory)
    {
        _broadcasterFactory = broadcasterFactory;
    }

    public void Emit(LogEvent logEvent)
    {
        var broadcaster = _broadcasterFactory();
        //Console.WriteLine(logEvent.RenderMessage());
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

        var envelope = new WsEnvelope
        {
            EventType = WsEventType.Log,
            Payload = JsonSerializer.SerializeToElement(payload),
        };

        _ = Task.Run(async () =>
        {
            try
            {
                await broadcaster.BroadcastAsync(envelope);
            }
            catch(Exception e)
            {
                //_broadcaster.Logger.LogError(e, "Error broadcasting log event");
            }
        });
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