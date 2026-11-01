using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.History;
using NicheImageRipper.Sdk.Configuration;
using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Service.Models.Responses;
using NicheImageRipper.Service.Singletons;
using Config = NicheImageRipper.Service.Models.Configs.Config;

namespace NicheImageRipper.Service.Tests;

/// <summary>Base for test fakes: override what a test uses; anything else fails loudly.</summary>
public abstract class NotImplementedRipperSingleton : INicheImageRipperSingleton
{
    public event Action? QueueUpdated { add { } remove { } }
    public event Action<int, int>? ProgressChanged { add { } remove { } }

    public virtual string[] GetQueueSnapshot() => throw new NotImplementedException();
    public virtual string? GetCurrentUrl() => throw new NotImplementedException();
    public virtual string? SaveQueueToFileAndClear() => throw new NotImplementedException();
    public virtual RejectedUrlsInfo Queue(string urls) => throw new NotImplementedException();
    public virtual void ForceQueue(string urls) => throw new NotImplementedException();
    public virtual void Dequeue(string[] urls) => throw new NotImplementedException();
    public virtual bool Rip() => throw new NotImplementedException();
    public virtual bool IsRipping => throw new NotImplementedException();
    public virtual bool Paused => throw new NotImplementedException();
    public virtual bool Pause() => throw new NotImplementedException();
    public virtual bool Resume() => throw new NotImplementedException();

    public virtual IEnumerable<HistoryEntry> GetHistory(int start, int offset, HistoryFilter? filter = null) =>
        throw new NotImplementedException();

    public virtual int GetHistoryCount() => throw new NotImplementedException();
    public virtual GeneralConfig GetConfig() => throw new NotImplementedException();
    public virtual void UpdateConfig(Config config) => throw new NotImplementedException();
    public virtual List<string> GetSiteNames() => throw new NotImplementedException();
    public virtual Dictionary<string, GalleryUpdateSupport> GetGalleryUpdateSupport() => throw new NotImplementedException();
    public virtual Version GetVersion() => throw new NotImplementedException();

    public virtual Task<UpdateCheckResponse> CheckForUpdate(CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public virtual void ClearCache() => throw new NotImplementedException();
    public virtual Task Save(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public virtual void LoadUrls(List<string> urls) => throw new NotImplementedException();
    public virtual Task StopAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
}
