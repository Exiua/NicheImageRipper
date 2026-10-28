using System.Text.Json.Serialization;

namespace NicheImageRipper.Service.Models.WebSocket;

/// <summary>Log levels a WebSocket client can subscribe to. Values match Serilog's <c>LogEventLevel</c>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<WsLogLevel>))]
public enum WsLogLevel
{
    Verbose,
    Debug,
    Information,
    Warning,
    Error,
    Fatal,
}
