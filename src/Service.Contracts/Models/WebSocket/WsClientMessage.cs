using System.Text.Json.Serialization;

namespace NicheImageRipper.Service.Models.WebSocket;

/// <summary>A message a client sends to the service over <c>/ws/events</c>.</summary>
public sealed class WsClientMessage
{
    public required WsClientMessageType Type { get; set; }

    /// <summary>For <see cref="WsClientMessageType.SetLogLevel"/>: the lowest level of log events to receive.</summary>
    public WsLogLevel? LogLevel { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter<WsClientMessageType>))]
public enum WsClientMessageType
{
    SetLogLevel,
}
