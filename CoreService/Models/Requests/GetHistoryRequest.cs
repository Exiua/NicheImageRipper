using Core.History;

namespace CoreService.Models.Requests;

public class GetHistoryRequest
{
    public int Start { get; set; }
    public int Offset { get; set; }
    public HistoryFilter? Filter { get; set; }
}