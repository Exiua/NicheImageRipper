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

public interface IBackendConnector
{
    public string ApiKey { get; set; }
    public string EndpointUri { get; set; }

    event Action<LogEntryModel>? LogReceived;

    public Task<string[]> GetQueueSnapshotAsync(CancellationToken cancellationToken = default);
    public Task<int> GetQueueCountAsync(CancellationToken cancellationToken = default);

    public Task<RejectedUrlsInfo> QueueUrlsAsync(
        string urls,
        CancellationToken cancellationToken = default);

    public Task DequeueUrlsAsync(
        string[] urls,
        CancellationToken cancellationToken = default);

    public Task<bool> RipAsync(CancellationToken cancellationToken = default);
    public Task<bool> PauseAsync(CancellationToken cancellationToken = default);
    public Task<bool> ResumeAsync(CancellationToken cancellationToken = default);
    public Task<bool> GetPausedStateAsync(CancellationToken cancellationToken = default);
    public Task<bool> GetIsRippingStateAsync(CancellationToken cancellationToken = default);

    public Task<IEnumerable<HistoryEntry>> GetHistoryAsync(
        GetHistoryRequest request,
        CancellationToken cancellationToken = default);

    public Task<int> GetHistoryCountAsync(CancellationToken cancellationToken = default);

    public Task ConnectLogsAsync(CancellationToken cancellationToken = default);
    public Task DisconnectLogsAsync(CancellationToken cancellationToken = default);
    public Task<GeneralConfig> GetConfigAsync(CancellationToken cancellationToken = default);
    public Task UpdateConfigAsync(Config config, CancellationToken cancellationToken = default);
}