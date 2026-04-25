using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core;
using Core.DataStructures;
using Core.History;
using Core.Utility;

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

    public Task<bool> Resume()
    {
        return Task.FromResult(_ripper.Resume());
    }

    public Task<bool> Pause()
    {
        return Task.FromResult(_ripper.Pause());
    }

    public Task<IEnumerable<string>> GetUrlQueue()
    {
        return Task.FromResult<IEnumerable<string>>(_ripper.UrlQueue);
    }

    public Task<RejectedUrlsInfo> QueueUrls(string url)
    {
        return Task.FromResult(_ripper.QueueUrls(url));
    }

    public Task DequeueUrls(IEnumerable<string> url)
    {
        _ripper.DequeueUrls(url);
        return Task.CompletedTask;
    }

    public Task ForceQueueUrl(string url)
    {
        _ripper.ForceQueueUrl(url);
        return Task.CompletedTask;
    }

    public Task RequeueUrls(RejectedUrlsInfo rejectedUrls)
    {
        _ripper.RequeueUrls(rejectedUrls);
        return Task.CompletedTask;
    }

    public Task LoadUrlFile(string path)
    {
        _ripper.LoadUrlFile(path);
        return Task.CompletedTask;
    }

    public Task SaveData()
    {
        _ripper.SaveData();
        return Task.CompletedTask;
    }

    public Task<Version> GetCoreVersion()
    {
        return Task.FromResult(NicheImageRipper.Version);
    }
    
    public Task<List<HistoryEntry>> GetHistoryPage(int start, int offset, HistoryFilter? filter = null)
    {
        return Task.FromResult(NicheImageRipper.GetHistoryPage(start, offset, filter));
    }

    public Task ClearCache()
    {
        NicheImageRipper.ClearCache();
        return Task.CompletedTask;
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