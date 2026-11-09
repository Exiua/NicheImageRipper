using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.History;
using NicheImageRipper.Service.Models.Responses;

namespace NicheImageRipper.Gui.Services;

public interface IRipperClient : IAsyncInitialization, IDisposable
{
    public event Action? OnUrlQueueUpdated;
    public event Action<int, int>? OnProgressChanged;

    /// <summary>
    ///     Raised when ripping stops before the queue is finished (e.g. the disk is full). The thin client doesn't
    ///     raise it: the backend reports the stop in the log stream instead.
    /// </summary>
    public event Action<RipStopReason>? OnRipStopped;

    public bool Connected { get; }

    public Task<bool> Rip(CancellationToken cancellationToken = default);
    public Task<bool> Resume(CancellationToken cancellationToken = default);
    public Task<bool> Pause(CancellationToken cancellationToken = default);
    /// <summary>
    ///     Skips the file currently downloading, or the one a failed download stopped the rip at, leaving an empty
    ///     <c>&lt;filename&gt;.skip</c> marker.
    /// </summary>
    /// <returns>True if there was a file to skip.</returns>
    public Task<bool> SkipCurrentFile(CancellationToken cancellationToken = default);
    public Task<IEnumerable<string>> GetUrlQueue(CancellationToken cancellationToken = default);
    public Task<int> GetUrlQueueCount(CancellationToken cancellationToken = default);
    /// <returns>URL currently being ripped, or null when idle</returns>
    public Task<string?> GetCurrentUrl(CancellationToken cancellationToken = default);
    public Task<RejectedUrlsInfo> QueueUrls(string url, CancellationToken cancellationToken = default);
    public Task DequeueUrls(IEnumerable<string> url, CancellationToken cancellationToken = default);
    public Task ForceQueueUrl(string url, CancellationToken cancellationToken = default);
    public Task RequeueUrls(RejectedUrlsInfo rejectedUrls, CancellationToken cancellationToken = default);
    public Task LoadUrlFile(string path, CancellationToken cancellationToken = default);
    /// <summary>Saves the queue to a new unfinished-rip file, clears the queue, and starts a fresh unfinished file.</summary>
    /// <returns>Path of the saved file (on the backend for the thin client), or null if the queue was empty</returns>
    public Task<string?> SaveQueueToFileAndClear(CancellationToken cancellationToken = default);
    public Task SaveData(CancellationToken cancellationToken = default);
    public Task<Version> GetCoreVersion(CancellationToken cancellationToken = default);
    public Task<UpdateCheckResponse> CheckForUpdate(CancellationToken cancellationToken = default);
    public Task<List<HistoryEntry>> GetHistoryPage(int start, int offset, HistoryFilter? filter = null, CancellationToken cancellationToken = default);
    public Task ClearCache(CancellationToken cancellationToken = default);
    public Task<int> GetHistoryCount(CancellationToken cancellationToken = default);
    public Task SkipCurrentEntry(CancellationToken cancellationToken = default);
}