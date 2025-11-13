namespace Core.History;

public class HistoryUrlFilter : HistoryFilter
{
    public override HistoryFilterType FilterType { get; } = HistoryFilterType.Url;
    
    public string Url { get; }
    
    public HistoryUrlFilter(string url)
    {
        Url = url;
    }
}