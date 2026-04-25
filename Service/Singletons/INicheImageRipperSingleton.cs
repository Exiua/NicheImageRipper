using Core.Configuration;
using Core.DataStructures;
using Core.History;
using Service.Models.Dtos;
using Config = Service.Models.Configs.Config;

namespace Service.Singletons;

public interface INicheImageRipperSingleton
{
    public string[] GetQueueSnapshot();
    public RejectedUrlsInfo Queue(string urls);
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
}