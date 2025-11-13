namespace Core.DataStructures;

public class RejectedUrlsInfo
{
    public List<RejectedUrlInfo> Urls { get; private set; } = [];
    public int StartIndex { get; }
    
    public int Count => Urls.Count;

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