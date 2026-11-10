using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.History;
using NicheImageRipper.Sdk.Configuration;
using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Service.Models.Responses;
using Config = NicheImageRipper.Service.Models.Configs.Config;

namespace NicheImageRipper.Service.Singletons;

public interface INicheImageRipperSingleton
{
    public event Action? QueueUpdated;
    public event Action<int, int>? ProgressChanged;
    
    public string[] GetQueueSnapshot();
    public string? GetCurrentUrl();
    public string? SaveQueueToFileAndClear();
    public RejectedUrlsInfo Queue(string urls);
    public void ForceQueue(string urls);
    public void Dequeue(string[] urls);
    public bool Rip();
    public bool IsRipping { get; }
    public bool Paused { get; }
    public bool Pause();
    public bool Resume();
    public Task<bool> SkipCurrentFile(CancellationToken cancellationToken = default);
    public IEnumerable<HistoryEntry> GetHistory(int start, int offset, HistoryFilter? filter = null);
    public int GetHistoryCount();
    public GeneralConfig GetConfig();
    /// <summary>Applies the non-null settings in <paramref name="config"/> and saves them.</summary>
    public void UpdateConfig(Config config);
    public List<string> GetSiteNames();
    /// <summary>The update strategies of every updatable parser, keyed by parser name.</summary>
    public Dictionary<string, GalleryUpdateSupport> GetGalleryUpdateSupport();
    /// <summary>The custom settings schema of every parser that declares one, keyed by parser name.</summary>
    public Dictionary<string, SiteSettingsSchema> GetSiteSettingsSchemas();
    public Version GetVersion();
    public Task<UpdateCheckResponse> CheckForUpdate(CancellationToken cancellationToken = default);
    public void ClearCache();
    public Task Save(CancellationToken cancellationToken = default);
    public void LoadUrls(List<string> urls);

    /// <summary>Cancels a rip in progress, waits for it to stop, and saves the queue, rip position and settings.</summary>
    public Task StopAsync(CancellationToken cancellationToken = default);
}