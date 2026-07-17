using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.History;

namespace NicheImageRipper.Gui.Services;

public interface IRipperClient : IAsyncInitialization, IDisposable
{
    public event Action? OnUrlQueueUpdated;
    public event Action<int, int>? OnProgressChanged;

    public int UrlQueueCount { get; }
    public bool Connected { get; }

    public Task<bool> Rip(CancellationToken cancellationToken = default);
    public Task<bool> Resume(CancellationToken cancellationToken = default);
    public Task<bool> Pause(CancellationToken cancellationToken = default);
    public Task<IEnumerable<string>> GetUrlQueue(CancellationToken cancellationToken = default);
    public Task<RejectedUrlsInfo> QueueUrls(string url, CancellationToken cancellationToken = default);
    public Task DequeueUrls(IEnumerable<string> url, CancellationToken cancellationToken = default);
    public Task ForceQueueUrl(string url, CancellationToken cancellationToken = default);
    public Task RequeueUrls(RejectedUrlsInfo rejectedUrls, CancellationToken cancellationToken = default);
    public Task LoadUrlFile(string path, CancellationToken cancellationToken = default);
    public Task SaveData(CancellationToken cancellationToken = default);
    public Task<Version> GetCoreVersion(CancellationToken cancellationToken = default);
    public Task<List<HistoryEntry>> GetHistoryPage(int start, int offset, HistoryFilter? filter = null, CancellationToken cancellationToken = default);
    public Task ClearCache(CancellationToken cancellationToken = default);
    public Task<int> GetHistoryCount(CancellationToken cancellationToken = default);
    public Task SkipCurrentEntry(CancellationToken cancellationToken = default);
}