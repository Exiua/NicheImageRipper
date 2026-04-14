using Newtonsoft.Json;

namespace Core.DataStructures;

public class RejectedUrlsInfo(int startIndex)
{
    public List<RejectedUrlInfo> Urls { get; private set; } = [];
    public int StartIndex { get; } = startIndex;

    [JsonIgnore]
    public int Count => Urls.Count;
    [JsonIgnore]
    public bool HasRejectedUrls => Urls.Count > 0;

    public RejectedUrlsInfo WithRejectedUrls(List<RejectedUrlInfo> rejectedUrls)
    {
        Urls = rejectedUrls;
        return this;
    }
}