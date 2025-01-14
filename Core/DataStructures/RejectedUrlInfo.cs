using Core.Enums;

namespace Core.DataStructures;

public class RejectedUrlInfo(string url, QueueFailureReason reason, int index = -1)
{
    public string Url { get; set; } = url;
    public QueueFailureReason Reason { get; set; } = reason;
    public int Index { get; set; } = index;
}