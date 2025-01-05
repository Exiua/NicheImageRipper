using OpenQA.Selenium.BiDi.Modules.Network;
using Serilog;

namespace Core.DataStructures.VideoCapturers;

public abstract class PlaylistCapturer
{
    private readonly Dictionary<string, List<string>> _videoUrls = new();
    private readonly HashSet<string> _seenIds = [];
    
    public void CaptureHook(ResponseCompletedEventArgs e)
    {
        //Log.Debug("New network response received: {url}", url);
        if (!ResponseIsInteresting(e))
        {
            return;
        }
        
        var url = e.Response.Url;
        var id = GetId(url);
        //Log.Debug("[{id}]: {url}", id, url);
        if (!_videoUrls.TryGetValue(id, out var value))
        {
            value = [];
            _videoUrls[id] = value;
        }

        value.Add(url);
    }
    
    protected abstract bool ResponseIsInteresting(ResponseCompletedEventArgs e);

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
    
    protected static string GetUrlParameterValue(string url, string parameter)
    {
        return url.Split($"{parameter}=")[1].Split("&")[0];
    }
}