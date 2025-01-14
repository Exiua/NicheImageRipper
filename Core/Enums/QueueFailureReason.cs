namespace Core.Enums;

public enum QueueFailureReason
{
    None,
    AlreadyQueued,
    NotSupported,
    PreviouslyProcessed,
}