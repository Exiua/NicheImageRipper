namespace Core.History;

public class HistoryDateFilter : HistoryFilter
{
    public override HistoryFilterType FilterType { get; } = HistoryFilterType.DateStart;
    
    public DateTime Date { get; }

    public HistoryDateFilter(DateTime date)
    {
        Date = date;
    }

    public HistoryDateFilter(DateTime date, HistoryFilterType type)
    {
        Date = date;
        if (type != HistoryFilterType.DateStart && type != HistoryFilterType.DateEnd)
        {
            throw new ArgumentException("Invalid filter type for date filter.", nameof(type));
        }

        FilterType = type;
    }
}