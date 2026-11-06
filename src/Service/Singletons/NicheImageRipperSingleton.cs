using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.History;
using NicheImageRipper.Sdk.Configuration;
using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Service.Models.Responses;
using Config = NicheImageRipper.Service.Models.Configs.Config;

namespace NicheImageRipper.Service.Singletons;
public sealed class NicheImageRipperSingleton : INicheImageRipperSingleton, IDisposable
{
    private readonly NicheImageRipper.Core.NicheImageRipper _ripper;
    private readonly Lock _ripperLock = new();
    private readonly Lock _configLock = new();
    private readonly ILogger<NicheImageRipperSingleton> _logger;
    private bool _isRipping;
    private CancellationTokenSource? _ripCancellation;
    private Task _ripTask = Task.CompletedTask;
    public event Action? QueueUpdated;
    public event Action<int, int>? ProgressChanged;
    public NicheImageRipperSingleton(ILogger<NicheImageRipperSingleton> logger)
    {
        _logger = logger;
        _ripper = new NicheImageRipper.Core.NicheImageRipper();
        _ripper.OnProgressChanged += (current, total) => ProgressChanged?.Invoke(current, total);
        _ripper.OnUrlQueueUpdated += () => QueueUpdated?.Invoke();
    }

    public string[] GetQueueSnapshot()
    {
        using (_ripperLock.EnterScope())
        {
            return _ripper.GetUrlQueueSnapshot().ToArray();
        }
    }

    public string? GetCurrentUrl()
    {
        return _ripper.CurrentUrl;
    }

    public string? SaveQueueToFileAndClear()
    {
        using (_ripperLock.EnterScope())
        {
            return _ripper.SaveQueueToFileAndClear();
        }
    }

    public RejectedUrlsInfo Queue(string urls)
    {
        using (_ripperLock.EnterScope())
        {
            return _ripper.QueueUrls(urls);
        }
    }

    public void ForceQueue(string urls)
    {
        using (_ripperLock.EnterScope())
        {
            _ripper.ForceQueueUrls(urls);
        }
    }

    public void Dequeue(string[] urls)
    {
        using (_ripperLock.EnterScope())
        {
            _ripper.DequeueUrls(urls);
        }
    }

    public bool Rip()
    {
        if (Interlocked.Exchange(ref _isRipping, true))
        {
            return false;
        }

        var cancellation = new CancellationTokenSource();
        _ripCancellation = cancellation;
        _ripTask = Task.Run(async () =>
        {
            try
            {
                var stopReason = await _ripper.Rip(cancellation.Token);
                if (stopReason == RipStopReason.OutOfDiskSpace)
                {
                    _logger.LogError("Ripping stopped: not enough disk space on the backend. The queue was kept; " +
                                     "free up space and start ripping again.");
                }
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                _logger.LogInformation("Ripping stopped because the service is shutting down.");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An error occurred while ripping.");
            }
            finally
            {
                Interlocked.Exchange(ref _isRipping, false);
            }
        });
        return true;
    }

    public bool IsRipping => Volatile.Read(ref _isRipping);
    public bool Paused => _ripper.Paused;

    public bool Pause()
    {
        if (!IsRipping || Paused)
        {
            return false;
        }

        _ripper.Pause();
        return true;
    }

    public bool Resume()
    {
        if (!IsRipping || !Paused)
        {
            return false;
        }

        _ripper.Resume();
        return true;
    }

    public IEnumerable<HistoryEntry> GetHistory(int start, int offset, HistoryFilter? filter = null)
    {
        return NicheImageRipper.Core.NicheImageRipper.GetHistoryPage(start, offset, filter);
    }

    public int GetHistoryCount()
    {
        return NicheImageRipper.Core.NicheImageRipper.GetHistoryCount();
    }

    public GeneralConfig GetConfig()
    {
        return Core.NicheImageRipper.Config;
    }

    public void UpdateConfig(Config config)
    {
        using (_configLock.EnterScope())
        {
            ApplyConfig(config, Core.NicheImageRipper.Config);
            Core.NicheImageRipper.Config.SaveConfig();
        }
    }

    private static void ApplyConfig(Config config, GeneralConfig existingConfig)
    {
        if (config.UserAgent is not null)
        {
            existingConfig.UserAgent = config.UserAgent;
        }

        if (config.SavePath is not null)
        {
            existingConfig.SavePath = config.SavePath;
        }

        if (config.Theme is not null)
        {
            existingConfig.Theme = config.Theme;
        }

        if (config.FilenameScheme is not null)
        {
            existingConfig.FilenameScheme = config.FilenameScheme.Value;
        }

        if (config.UnzipProtocol is not null)
        {
            existingConfig.UnzipProtocol = config.UnzipProtocol.Value;
        }

        if (config.PostDownloadAction is not null)
        {
            existingConfig.PostDownloadAction = config.PostDownloadAction;
        }

        if (config.ReRipBehavior is not null)
        {
            existingConfig.ReRipBehavior = config.ReRipBehavior.Value;
        }

        if (config.GalleryUpdateMode is not null)
        {
            existingConfig.GalleryUpdateMode = config.GalleryUpdateMode.Value;
        }

        if (config.SubParserOverrides is not null)
        {
            existingConfig.SubParserOverrides = config.SubParserOverrides.Value;
        }

        if (config.LiveHistory is not null)
        {
            existingConfig.LiveHistory = config.LiveHistory.Value;
        }

        if (config.SkipFailedDownloads is not null)
        {
            existingConfig.SkipFailedDownloads = config.SkipFailedDownloads.Value;
        }

        if (config.NumThreads is not null)
        {
            existingConfig.NumThreads = config.NumThreads.Value;
        }

        if (config.MaxRetries is not null)
        {
            existingConfig.MaxRetries = config.MaxRetries.Value;
        }

        if (config.RetryDelay is not null)
        {
            existingConfig.RetryDelay = config.RetryDelay.Value;
        }

        if (config.FlareSolverrUri is not null)
        {
            existingConfig.FlareSolverrUri = config.FlareSolverrUri;
        }

        if (config.CloseFlareSolverrSession is not null)
        {
            existingConfig.CloseFlareSolverrSession = config.CloseFlareSolverrSession.Value;
        }

        if (config.CSWebDriverUri is not null)
        {
            existingConfig.CSWebDriverUri = config.CSWebDriverUri;
        }

        if (config.SaveUnfinishedUrls is not null)
        {
            existingConfig.SaveUnfinishedUrls = config.SaveUnfinishedUrls.Value;
        }

        if (config.Sites is not null)
        {
            // Edit a copy and swap it in: rips in progress may be reading the current dictionary
            var sites = new Dictionary<string, SiteConfig>(existingConfig.Sites, StringComparer.OrdinalIgnoreCase);
            foreach (var (key, incoming) in config.Sites)
            {
                if (incoming is null)
                {
                    sites.Remove(key);
                }
                else
                {
                    sites[key] = incoming;
                }
            }

            existingConfig.Sites = sites;
        }
    }

    public List<string> GetSiteNames()
    {
        return Core.NicheImageRipper.GetParserNames();
    }

    public Dictionary<string, GalleryUpdateSupport> GetGalleryUpdateSupport()
    {
        return Core.NicheImageRipper.GetGalleryUpdateSupport();
    }

    public async Task<UpdateCheckResponse> CheckForUpdate(CancellationToken cancellationToken = default)
    {
        var isLatest = await _ripper.IsLatestVersion(cancellationToken);
        var latestVersion = isLatest is null ? null : await _ripper.GetLatestVersionAsync(cancellationToken);
        return new UpdateCheckResponse(Core.NicheImageRipper.Version, latestVersion, isLatest);
    }

    public Version GetVersion()
    {
        return Core.NicheImageRipper.Version;
    }

    public void ClearCache()
    {
        Core.NicheImageRipper.ClearCache();
    }

    public Task Save(CancellationToken cancellationToken = default)
    {
        return _ripper.SaveData(cancellationToken: cancellationToken);
    }

    public void LoadUrls(List<string> urls)
    {
        using (_ripperLock.EnterScope())
        {
            _ripper.LoadUrls(urls);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_ripCancellation is { } ripCancellation)
        {
            await ripCancellation.CancelAsync();
        }

        try
        {
            await _ripTask.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("The rip didn't stop in time; saving anyway.");
        }

        await _ripper.SaveData(CancellationToken.None);
    }

    public void Dispose()
    {
        _ripCancellation?.Dispose();
        _ripper.Dispose();
    }
}
