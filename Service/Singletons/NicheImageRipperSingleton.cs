using System.Text.Json;
using NicheImageRipper.Core.Configuration;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.History;
using NicheImageRipper.Service.Utilities.ExtensionMethods;
using Sdk.Configuration;
using Config = NicheImageRipper.Service.Models.Configs.Config;
using SettingsOverride = Sdk.Configuration.SettingsOverride;

namespace NicheImageRipper.Service.Singletons;
public class NicheImageRipperSingleton : INicheImageRipperSingleton
{
    private readonly NicheImageRipper.Core.NicheImageRipper _ripper;
    private readonly Lock _ripperLock = new();
    private readonly ILogger<NicheImageRipperSingleton> _logger;
    private bool _isRipping;
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
            return _ripper.UrlQueue.Select(u => u).ToArray();
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

        Task.Run(async () =>
        {
            try
            {
                await _ripper.Rip();
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

    public bool IsRipping => _isRipping;
    public bool Paused => _ripper.Paused;

    public bool Pause()
    {
        if (!_isRipping || Paused)
        {
            return false;
        }

        _ripper.Pause();
        return true;
    }

    public bool Resume()
    {
        if (!_isRipping || !Paused)
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
        var existingConfig = Core.NicheImageRipper.Config;
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
            existingConfig.PostDownloadAction = config.PostDownloadAction.Value;
        }

        if (config.AskToReRip is not null)
        {
            existingConfig.AskToReRip = config.AskToReRip.Value;
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

        if (config.Logins is not null)
        {
            foreach (var (key, incoming) in config.Logins)
            {
                if (incoming is null)
                {
                    continue;
                }

                if (!existingConfig.Logins.TryGetValue(key, out var existing))
                {
                    existing = new Credentials { Username = "", Password = "" };
                    existingConfig.Logins[key] = existing;
                }

                existing.UpdateCredentials(incoming);
            }
        }

        if (config.Keys is not null)
        {
            foreach (var (key, value) in config.Keys)
            {
                if (value is not null)
                {
                    existingConfig.Keys[key] = value;
                }
            }
        }

        if (config.Cookies is not null)
        {
            foreach (var (key, value) in config.Cookies)
            {
                if (value is not null)
                {
                    existingConfig.Cookies[key] = value;
                }
            }
        }

        if (config.Custom is not null)
        {
            foreach (var (key, value) in config.Custom)
            {
                if (value.ValueKind != JsonValueKind.Null)
                {
                    existingConfig.Custom[key] = value;
                }
            }
        }

        if (config.ParserSpecificSettingsOverrides is not null)
        {
            foreach (var(key, value)in config.ParserSpecificSettingsOverrides)
            {
                SettingsOverride settingsOverride;
                if (existingConfig.ParserSpecificSettingsOverrides.TryGetValue(key, out var @override))
                {
                    settingsOverride = @override;
                }
                else
                {
                    settingsOverride = new SettingsOverride();
                    existingConfig.ParserSpecificSettingsOverrides[key] = settingsOverride;
                }

                if (value.FilenameScheme is not null)
                {
                    settingsOverride.FilenameScheme = value.FilenameScheme.Value;
                }
            }
        }
    }

    public Version GetVersion()
    {
        return NicheImageRipper.Core.NicheImageRipper.Version;
    }

    public void ClearCache()
    {
        NicheImageRipper.Core.NicheImageRipper.ClearCache();
    }

    public Task Save(CancellationToken cancellationToken = default)
    {
        return _ripper.SaveData(cancellationToken: cancellationToken);
    }

    public void LoadUrls(List<string> urls)
    {
        _ripper.LoadUrls(urls);
    }
}