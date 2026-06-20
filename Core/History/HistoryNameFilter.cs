namespace NicheImageRipper.Core.History;

public class HistoryNameFilter : HistoryFilter
{
    public override HistoryFilterType FilterType => HistoryFilterType.DirectoryName;
    
    public string Name { get; }

    public HistoryNameFilter(string name)
    {
        Name = name;
    }
}