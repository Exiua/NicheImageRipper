using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.Configuration;
using Core.DataStructures;
using Gui.Models;
using Service.Models.Requests;
using Config = Service.Models.Configs.Config;

namespace Gui.Services.Thin;

public interface IBackendConnector : IAsyncInitialization
{
    public string ApiKey { get; set; }
    public string EndpointUri { get; set; }

    event Action<LogEntryModel>? LogReceived;
    public event Action? QueueUpdated;
    public event Action<int, int>? ProgressChanged;
    public event Action<string>? UnknownEventReceived;

    public Task<string[]> GetQueueSnapshotAsync(CancellationToken cancellationToken = default);
    public Task<int> GetQueueCountAsync(CancellationToken cancellationToken = default);

    public Task<RejectedUrlsInfo> QueueUrlsAsync(
        string urls,
        bool force = false, CancellationToken cancellationToken = default);

    public Task DequeueUrlsAsync(
        string[] urls,
        CancellationToken cancellationToken = default);
    
    public Task LoadUrlsAsync(IEnumerable<string> urls, CancellationToken cancellationToken = default);

    public Task<bool> RipAsync(CancellationToken cancellationToken = default);
    public Task<bool> PauseAsync(CancellationToken cancellationToken = default);
    public Task<bool> ResumeAsync(CancellationToken cancellationToken = default);
    public Task<bool> GetPausedStateAsync(CancellationToken cancellationToken = default);
    public Task<bool> GetIsRippingStateAsync(CancellationToken cancellationToken = default);

    public Task<IEnumerable<HistoryEntry>> GetHistoryAsync(
        GetHistoryRequest request,
        CancellationToken cancellationToken = default);

    public Task<int> GetHistoryCountAsync(CancellationToken cancellationToken = default);

    public Task ConnectWebSocketAsync(CancellationToken cancellationToken = default);
    public Task DisconnectWebSocketAsync(CancellationToken cancellationToken = default);
    public Task<GeneralConfig> GetConfigAsync(CancellationToken cancellationToken = default);
    public Task UpdateConfigAsync(Config config, CancellationToken cancellationToken = default);
    public Task<Version> GetCurrentVersionAsync(CancellationToken cancellationToken = default);
    public Task ClearCacheAsync(CancellationToken cancellationToken = default);
    public Task SaveStateAsync(CancellationToken cancellationToken = default);
}