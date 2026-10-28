using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Gui.Models;
using NicheImageRipper.Sdk.Configuration;
using NicheImageRipper.Service.Models.Requests;
using NicheImageRipper.Service.Models.Responses;
using NicheImageRipper.Service.Models.WebSocket;
using Config = NicheImageRipper.Service.Models.Configs.Config;

namespace NicheImageRipper.Gui.Services.Thin;

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
    public Task<string?> GetCurrentUrlAsync(CancellationToken cancellationToken = default);

    public Task<RejectedUrlsInfo> QueueUrlsAsync(
        string urls,
        bool force = false, CancellationToken cancellationToken = default);

    public Task DequeueUrlsAsync(
        string[] urls,
        CancellationToken cancellationToken = default);
    
    public Task LoadUrlsAsync(IEnumerable<string> urls, CancellationToken cancellationToken = default);
    public Task<string?> SaveQueueToFileAndClearAsync(CancellationToken cancellationToken = default);

    public Task<bool> RipAsync(CancellationToken cancellationToken = default);
    public Task<bool> PauseAsync(CancellationToken cancellationToken = default);
    public Task<bool> ResumeAsync(CancellationToken cancellationToken = default);
    public Task<bool> GetPausedStateAsync(CancellationToken cancellationToken = default);
    public Task<bool> GetIsRippingStateAsync(CancellationToken cancellationToken = default);

    public Task<IEnumerable<HistoryEntry>> GetHistoryAsync(
        GetHistoryRequest request,
        CancellationToken cancellationToken = default);

    public Task<int> GetHistoryCountAsync(CancellationToken cancellationToken = default);

    /// <summary>Lowest level of service log events the WebSocket receives.</summary>
    public WsLogLevel LogLevel { get; }

    /// <summary>
    ///     Changes <see cref="LogLevel"/>; sent to the service right away when connected, otherwise used on the next
    ///     connect.
    /// </summary>
    public Task SetLogLevelAsync(WsLogLevel level, CancellationToken cancellationToken = default);

    public Task ConnectWebSocketAsync(CancellationToken cancellationToken = default);
    public Task DisconnectWebSocketAsync(CancellationToken cancellationToken = default);
    public Task<GeneralConfig> GetConfigAsync(CancellationToken cancellationToken = default);
    public Task UpdateConfigAsync(Config config, CancellationToken cancellationToken = default);
    public Task<string[]> GetSiteNamesAsync(CancellationToken cancellationToken = default);
    public Task<Version> GetCurrentVersionAsync(CancellationToken cancellationToken = default);
    public Task<UpdateCheckResponse> CheckForUpdateAsync(CancellationToken cancellationToken = default);
    public Task ClearCacheAsync(CancellationToken cancellationToken = default);
    public Task SaveStateAsync(CancellationToken cancellationToken = default);
}