using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.DataStructures;
using Core.History;
using Core.Utility;
using Gui.Utility;
using Service.Models.Requests;

namespace Gui.Services.Thin;

public class RemoteRipperClient : IRipperClient
{
    private readonly IBackendConnector _backendConnector;

    public event Action? OnUrlQueueUpdated;
    public event Action<int, int>? OnProgressChanged;

    public int UrlQueueCount => _backendConnector.GetQueueCountAsync().Result;
    public bool Connected { get; set; }

    public RemoteRipperClient(IBackendConnector backendConnector, ILogTextSource logTextSource)
    {
        _backendConnector = backendConnector;
        _backendConnector.QueueUpdated += () => OnUrlQueueUpdated?.Invoke();
        _backendConnector.ProgressChanged += (current, total) => OnProgressChanged?.Invoke(current, total);
        _backendConnector.LogReceived += GuiLogBridge.Publish; 
    }
    
    public Task Rip(CancellationToken cancellationToken = default)
    {
        return _backendConnector.RipAsync(cancellationToken);
    }

    public Task<bool> Resume(CancellationToken cancellationToken = default)
    {
        return _backendConnector.ResumeAsync(cancellationToken);
    }

    public Task<bool> Pause(CancellationToken cancellationToken = default)
    {
        return _backendConnector.PauseAsync(cancellationToken);
    }

    public async Task<IEnumerable<string>> GetUrlQueue(CancellationToken cancellationToken = default)
    {
        var queue = await _backendConnector.GetQueueSnapshotAsync(cancellationToken);
        return queue;
    }

    public Task<RejectedUrlsInfo> QueueUrls(string url, CancellationToken cancellationToken = default)
    {
        return _backendConnector.QueueUrlsAsync(url, cancellationToken: cancellationToken);
    }

    public Task DequeueUrls(IEnumerable<string> url, CancellationToken cancellationToken = default)
    {
        return _backendConnector.DequeueUrlsAsync(url as string[] ?? url.ToArray(), cancellationToken);
    }

    public Task ForceQueueUrl(string url, CancellationToken cancellationToken = default)
    {
        return _backendConnector.QueueUrlsAsync(url, true, cancellationToken);
    }

    public Task RequeueUrls(RejectedUrlsInfo rejectedUrls, CancellationToken cancellationToken = default)
    {
        var urls = rejectedUrls.Urls.Aggregate("", (current, url) => current + url.Url);
        return _backendConnector.QueueUrlsAsync(urls, cancellationToken: cancellationToken);
    }

    public Task LoadUrlFile(string path, CancellationToken cancellationToken = default)
    {
        var loadedUrls = JsonUtility.Deserialize<List<string>>(path)!;
        return _backendConnector.LoadUrlsAsync(loadedUrls, cancellationToken);
    }

    public Task SaveData(CancellationToken cancellationToken = default)
    {
        return _backendConnector.SaveStateAsync(cancellationToken);
    }

    public Task<Version> GetCoreVersion(CancellationToken cancellationToken = default)
    {
        return _backendConnector.GetCurrentVersionAsync(cancellationToken);
    }

    public async Task<List<HistoryEntry>> GetHistoryPage(int start, int offset, HistoryFilter? filter = null, CancellationToken cancellationToken = default)
    {
        var request = new GetHistoryRequest
        {
            Start = start,
            Offset = offset,
            Filter = filter,
        };

        var historyEntries = await _backendConnector.GetHistoryAsync(request, cancellationToken);
        return historyEntries.ToList();
    }

    public Task ClearCache(CancellationToken cancellationToken = default)
    {
        return _backendConnector.ClearCacheAsync(cancellationToken);
    }

    public Task<int> GetHistoryCount(CancellationToken cancellationToken = default)
    {
        return _backendConnector.GetHistoryCountAsync(cancellationToken);
    }

    public Task SkipCurrentEntry(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}