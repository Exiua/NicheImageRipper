namespace Core.DataStructures;

public class RejectedUrlsInfo
{
    public List<RejectedUrlInfo> Urls { get; private set; } = [];
    public int StartIndex { get; }
    
    public int Count => Urls.Count;
    public bool HasRejectedUrls => Urls.Count > 0;

    public RejectedUrlsInfo(int startIndex)
    {
        StartIndex = startIndex;
    }
    
    public RejectedUrlsInfo WithRejectedUrls(List<RejectedUrlInfo> rejectedUrls)
    {
        Urls = rejectedUrls;
        return this;
    }
}