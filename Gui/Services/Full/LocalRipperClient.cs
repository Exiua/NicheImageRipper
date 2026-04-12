using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core;
using Core.DataStructures;

namespace Gui.Services.Full;

public class LocalRipperClient : IRipperClient
{
    private readonly NicheImageRipper _ripper;

    public int UrlQueueCount => _ripper.UrlQueue.Count;
    
    private bool _disposed;
    
    public event Action? OnUrlQueueUpdated;
    public event Action<int, int>? OnProgressChanged;

    public LocalRipperClient()
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
        _ripper.RequeueUrls(rejectedUrls);
    }

    public void LoadUrlFile(string path)
    {
        _ripper.LoadUrlFile(path);
    }

    public void SaveData()
    {
        _ripper.SaveData();
    }
    
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        
        _disposed = true;
        _ripper.Dispose();
        GC.SuppressFinalize(this);
    }
}