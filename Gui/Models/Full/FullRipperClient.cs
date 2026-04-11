using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core;
using Core.DataStructures;

namespace Gui.Models.Full;

public class FullRipperClient : IRipperClient
{
    private readonly NicheImageRipper _ripper;

    public int UrlQueueCount => _ripper.UrlQueue.Count;
    
    public event Action? OnUrlQueueUpdated;
    public event Action<int, int>? OnProgressChanged;

    public FullRipperClient()
    {
        _ripper = new NicheImageRipper();
        _ripper.OnUrlQueueUpdated += () => OnUrlQueueUpdated?.Invoke();
        _ripper.OnProgressChanged += (downloaded, total) => OnProgressChanged?.Invoke(downloaded, total);
    }
    
    public Task Rip()
    {
        return _ripper.Rip();
    }

    public bool Resume()
    {
        return _ripper.Resume();
    }

    public bool Pause()
    {
        return _ripper.Pause();
    }

    public IEnumerable<string> GetUrlQueue()
    {
        return _ripper.UrlQueue;
    }

    public RejectedUrlsInfo QueueUrls(string url)
    {
        return _ripper.QueueUrls(url);
    }

    public void DequeueUrls(IEnumerable<string> url)
    {
        _ripper.DequeueUrls(url);
    }

    public void ForceQueueUrl(string url)
    {
        _ripper.ForceQueueUrl(url);
    }

    public void RequeueUrls(RejectedUrlsInfo rejectedUrls)
    {
        throw new NotImplementedException();
    }

    public void LoadUrlFile(string path)
    {
        throw new NotImplementedException();
    }

    public void SaveData()
    {
        throw new NotImplementedException();
    }
    
    public void Dispose()
    {
        // TODO release managed resources here
    }
}