using System;
using System.Collections.Generic;
using System.Linq;
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
    
    public Task Rip()
    {
        return _backendConnector.RipAsync();
    }

    public Task<bool> Resume()
    {
        return _backendConnector.ResumeAsync();
    }

    public Task<bool> Pause()
    {
        return _backendConnector.PauseAsync();
    }

    public async Task<IEnumerable<string>> GetUrlQueue()
    {
        var queue = await _backendConnector.GetQueueSnapshotAsync();
        return queue;
    }

    public Task<RejectedUrlsInfo> QueueUrls(string url)
    {
        return _backendConnector.QueueUrlsAsync(url);
    }

    public Task DequeueUrls(IEnumerable<string> url)
    {
        return _backendConnector.DequeueUrlsAsync(url as string[] ?? url.ToArray());
    }

    public Task ForceQueueUrl(string url)
    {
        return _backendConnector.QueueUrlsAsync(url, true);
    }

    public Task RequeueUrls(RejectedUrlsInfo rejectedUrls)
    {
        var urls = rejectedUrls.Urls.Aggregate("", (current, url) => current + url.Url);
        return _backendConnector.QueueUrlsAsync(urls);
    }

    public Task LoadUrlFile(string path)
    {
        var loadedUrls = JsonUtility.Deserialize<List<string>>(path)!;
        return _backendConnector.LoadUrlsAsync(loadedUrls);
    }

    public Task SaveData()
    {
        return _backendConnector.SaveStateAsync();
    }

    public Task<Version> GetCoreVersion()
    {
        return _backendConnector.GetCurrentVersionAsync();
    }

    public async Task<List<HistoryEntry>> GetHistoryPage(int start, int offset, HistoryFilter? filter = null)
    {
        var request = new GetHistoryRequest
        {
            Start = start,
            Offset = offset,
            Filter = filter,
        };

        var historyEntries = await _backendConnector.GetHistoryAsync(request);
        return historyEntries.ToList();
    }

    public Task ClearCache()
    {
        return _backendConnector.ClearCacheAsync();
    }

    public Task<int> GetHistoryCount()
    {
        return _backendConnector.GetHistoryCountAsync();
    }

    public Task SkipCurrentEntry()
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}