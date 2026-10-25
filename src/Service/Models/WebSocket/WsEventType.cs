namespace NicheImageRipper.Service.Models.WebSocket;

public enum WsEventType : int
{
    Log,
    QueueUpdate,
    ProgressChange,
    Unknown = int.MaxValue,
}