using System.Text.Json;

namespace NicheImageRipper.Service.Models.WebSocket;

public sealed class WsEnvelope
{
    public required WsEventType EventType { get; set; }
    public JsonElement Payload { get; set; }
}

public sealed class ProgressChangedEvent
{
    public int Current { get; set; }
    public int Total { get; set; }
}