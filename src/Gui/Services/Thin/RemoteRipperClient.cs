using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.History;
using NicheImageRipper.Core.Utility;
using NicheImageRipper.Gui.Utility;
using NicheImageRipper.Sdk.Utility;
using NicheImageRipper.Service.Models.Requests;
using NicheImageRipper.Service.Models.Responses;

namespace NicheImageRipper.Gui.Services.Thin;

public class RemoteRipperClient : IRipperClient
{
    private readonly IBackendConnector _backendConnector;

    public event Action? OnUrlQueueUpdated;
    public event Action<int, int>? OnProgressChanged;
#pragma warning disable CS0067 // Never raised: the backend reports early stops in the log stream
    public event Action<RipStopReason>? OnRipStopped;
#pragma warning restore CS0067

    public bool Connected { get; set; }

    public RemoteRipperClient(IBackendConnector backendConnector, ILogTextSource logTextSource)
    {
        _backendConnector = backendConnector;
        _backendConnector.QueueUpdated += () => OnUrlQueueUpdated?.Invoke();
        _backendConnector.ProgressChanged += (current, total) => OnProgressChanged?.Invoke(current, total);
        _backendConnector.LogReceived += GuiLogBridge.Publish; 
    }
    
    public Task<bool> Rip(CancellationToken cancellationToken = default)
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

    public Task<bool> SkipCurrentFile(CancellationToken cancellationToken = default)
    {
        return _backendConnector.SkipCurrentFileAsync(cancellationToken);
    }

    public async Task<IEnumerable<string>> GetUrlQueue(CancellationToken cancellationToken = default)
    {
        var queue = await _backendConnector.GetQueueSnapshotAsync(cancellationToken);
        return queue;
    }

    public Task<int> GetUrlQueueCount(CancellationToken cancellationToken = default)
    {
        return _backendConnector.GetQueueCountAsync(cancellationToken);
    }

    public Task<string?> GetCurrentUrl(CancellationToken cancellationToken = default)
    {
        return _backendConnector.GetCurrentUrlAsync(cancellationToken);
    }

    public Task<CurrentRipFiles> GetCurrentRipFiles(CancellationToken cancellationToken = default)
    {
        return _backendConnector.GetCurrentRipFilesAsync(cancellationToken);
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
        if (!UrlListFile.TryRead(path, out var loadedUrls, out var error))
        {
            throw new InvalidDataException($"{path}: {error}");
        }

        return _backendConnector.LoadUrlsAsync(loadedUrls, cancellationToken);
    }

    public Task<string?> SaveQueueToFileAndClear(CancellationToken cancellationToken = default)
    {
        return _backendConnector.SaveQueueToFileAndClearAsync(cancellationToken);
    }

    public Task SaveData(CancellationToken cancellationToken = default)
    {
        return _backendConnector.SaveStateAsync(cancellationToken);
    }

    public Task<Version> GetCoreVersion(CancellationToken cancellationToken = default)
    {
        return _backendConnector.GetCurrentVersionAsync(cancellationToken);
    }

    public Task<UpdateCheckResponse> CheckForUpdate(CancellationToken cancellationToken = default)
    {
        return _backendConnector.CheckForUpdateAsync(cancellationToken);
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