using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.History;
using NicheImageRipper.Core.Utility;
using Sdk.Utility;

namespace NicheImageRipper.Gui.Services.Full;
public class LocalRipperClient : IRipperClient
{
    private readonly NicheImageRipper.Core.NicheImageRipper _ripper;
    public int UrlQueueCount => _ripper.UrlQueue.Count;
    public bool Connected => true;

    private bool _disposed;
    public event Action? OnUrlQueueUpdated;
    public event Action<int, int>? OnProgressChanged;
    public LocalRipperClient()
    {
        _ripper = new NicheImageRipper.Core.NicheImageRipper();
        _ripper.OnUrlQueueUpdated += () => OnUrlQueueUpdated?.Invoke();
        _ripper.OnProgressChanged += (downloaded, total) => OnProgressChanged?.Invoke(downloaded, total);
    }

    public Task<bool> Rip(CancellationToken cancellationToken = default)
    {
        return _ripper.Rip(cancellationToken);
    }

    public Task<bool> Resume(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_ripper.Resume());
    }

    public Task<bool> Pause(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_ripper.Pause());
    }

    public Task<IEnumerable<string>> GetUrlQueue(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IEnumerable<string>>(_ripper.UrlQueue);
    }

    public Task<RejectedUrlsInfo> QueueUrls(string url, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_ripper.QueueUrls(url));
    }

    public Task DequeueUrls(IEnumerable<string> url, CancellationToken cancellationToken = default)
    {
        _ripper.DequeueUrls(url);
        return Task.CompletedTask;
    }

    public Task ForceQueueUrl(string url, CancellationToken cancellationToken = default)
    {
        _ripper.ForceQueueUrls(url);
        return Task.CompletedTask;
    }

    public Task RequeueUrls(RejectedUrlsInfo rejectedUrls, CancellationToken cancellationToken = default)
    {
        _ripper.RequeueUrls(rejectedUrls);
        return Task.CompletedTask;
    }

    public Task LoadUrlFile(string path, CancellationToken cancellationToken = default)
    {
        var loadedUrls = JsonUtility.Deserialize<List<string>>(path)!;
        _ripper.LoadUrls(loadedUrls);
        return Task.CompletedTask;
    }

    public Task SaveData(CancellationToken cancellationToken = default)
    {
        return _ripper.SaveData(cancellationToken: cancellationToken);
    }

    public Task<Version> GetCoreVersion(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(NicheImageRipper.Core.NicheImageRipper.Version);
    }

    public Task<List<HistoryEntry>> GetHistoryPage(int start, int offset, HistoryFilter? filter = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(NicheImageRipper.Core.NicheImageRipper.GetHistoryPage(start, offset, filter));
    }

    public Task ClearCache(CancellationToken cancellationToken = default)
    {
        NicheImageRipper.Core.NicheImageRipper.ClearCache();
        return Task.CompletedTask;
    }

    public Task<int> GetHistoryCount(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(NicheImageRipper.Core.NicheImageRipper.GetHistoryCount());
    }

    public Task SkipCurrentEntry(CancellationToken cancellationToken = default)
    {
        return NicheImageRipper.Core.NicheImageRipper.SkipEntry(cancellationToken: cancellationToken);
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