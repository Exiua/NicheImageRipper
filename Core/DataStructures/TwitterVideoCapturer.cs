using OpenQA.Selenium.BiDi.Modules.Network;

namespace Core.DataStructures;

public class TwitterVideoCapturer
{
    private readonly Dictionary<string, List<string>> _videoUrls = new();
    private readonly HashSet<string> _seenIds = [];

    public void CaptureHook(ResponseCompletedEventArgs e)
    {
        var url = e.Response.Url;
        //Console.WriteLine("New Work Response received: " + url);
        if (!url?.Contains(".m3u8") ?? true)
        {
            return;
        }
        
        var tweetId = url.Split("/")[4];
        if (!_videoUrls.TryGetValue(tweetId, out var value))
        {
            value = [];
            _videoUrls[tweetId] = value;
        }

        value.Add(url);
    }
    
    public List<string> GetVideoLinks(string tweetId)
    {
        return _videoUrls.TryGetValue(tweetId, out var value) ? value : [];
    }

    public List<string> GetNewVideoLinks()
    {
        var newUrls = new List<string>();
        foreach (var key in _videoUrls.Keys.Where(key => _seenIds.Add(key)))
        {
            newUrls.AddRange(_videoUrls[key]);
        }
        
        return newUrls;
    }
    
    public void Flush()
    {
        _videoUrls.Clear();
        _seenIds.Clear();
    }
}