using NicheImageRipper.Core.Enums;

namespace NicheImageRipper.Service.Models.Dtos;

public class RejectedUrlInfoDto(string url, QueueFailureReason reason)
{
    public string Url { get; set; } = url;
    public QueueFailureReason Reason { get; set; } = reason;
}