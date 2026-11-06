namespace MegaSdk;

public enum MegaTransferState
{
    None = 0,
    Queued = 1,
    Active = 2,
    Paused = 3,
    Retrying = 4,
    Completing = 5,
    Completed = 6,
    Cancelled = 7,
    Failed = 8,
}
