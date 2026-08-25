using NicheImageRipper.Core.Configuration;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.History;
using Sdk.Configuration;
using Config = NicheImageRipper.Service.Models.Configs.Config;

namespace NicheImageRipper.Service.Singletons;

public interface INicheImageRipperSingleton
{
    public event Action? QueueUpdated;
    public event Action<int, int>? ProgressChanged;
    
    public string[] GetQueueSnapshot();
    public RejectedUrlsInfo Queue(string urls);
    public void ForceQueue(string urls);
    public void Dequeue(string[] urls);
    public bool Rip();
    public bool IsRipping { get; }
    public bool Paused { get; }
    public bool Pause();
    public bool Resume();
    public IEnumerable<HistoryEntry> GetHistory(int start, int offset, HistoryFilter? filter = null);
    public int GetHistoryCount();
    public GeneralConfig GetConfig();
    public void UpdateConfig(Config config);
    public Version GetVersion();
    public void ClearCache();
    public Task Save(CancellationToken cancellationToken = default);
    public void LoadUrls(List<string> urls);
}