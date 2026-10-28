namespace NicheImageRipper.Service.Models.WebSocket;

public enum WsEventType : int
{
    Log,
    QueueUpdate,
    ProgressChange,

    /// <summary>
    ///     Sent when the connection opens and after each <see cref="WsClientMessageType.SetLogLevel"/>, with the level
    ///     the service actually applied (<see cref="LogLevelChangedEvent"/>). It can be higher than requested when the
    ///     service doesn't send logs that detailed.
    /// </summary>
    LogLevelChanged,
    Unknown = int.MaxValue,
}
