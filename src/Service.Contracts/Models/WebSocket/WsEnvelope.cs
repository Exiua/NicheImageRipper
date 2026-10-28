using System.Text.Json;
using System.Text.Json.Serialization;

namespace NicheImageRipper.Service.Models.WebSocket;

public sealed class WsEnvelope
{
    public required WsEventType EventType { get; set; }
    /// <summary>Event data; left out for events that carry none, such as <see cref="WsEventType.QueueUpdate"/>.</summary>
    // An unset JsonElement can't be serialized, so it has to be skipped rather than written as null
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public JsonElement Payload { get; set; }
}

public sealed class ProgressChangedEvent
{
    public int Current { get; set; }
    public int Total { get; set; }
}

public sealed class LogLevelChangedEvent
{
    public WsLogLevel LogLevel { get; set; }
}
