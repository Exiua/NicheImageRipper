using OpenQA.Selenium.BiDi.Modules.Network;
using Serilog;

namespace Core.DataStructures;

public abstract class PlaylistCapturer
{
    private readonly Dictionary<string, List<string>> _videoUrls = new();
    private readonly HashSet<string> _seenIds = [];

    protected abstract string SearchPattern { get; }
    
    public void CaptureHook(ResponseCompletedEventArgs e)
    {
        var url = e.Response.Url;
        //Log.Debug("New network response received: {url}", url);
        if (!url.Contains(SearchPattern))
        {
            return;
        }
        
        var id = GetId(url);
        Log.Debug("[{id}]: {url}", id, url);
        if (!_videoUrls.TryGetValue(id, out var value))
        {
            value = [];
            _videoUrls[id] = value;
        }

        value.Add(url);
    }

    protected abstract string GetId(string url);
    
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