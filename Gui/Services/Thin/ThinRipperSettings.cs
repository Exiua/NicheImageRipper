using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Sdk.Configuration;
using NicheImageRipper.Sdk.Enums;
using Config = NicheImageRipper.Service.Models.Configs.Config;

namespace NicheImageRipper.Gui.Services.Thin;

public class ThinRipperSettings : IRipperSettings
{
    private readonly IBackendConnector _backendConnector;

    private GeneralConfig _config = new();
    private bool _initialized;
    private bool _loading;

    public ThinRipperSettings(IBackendConnector backendConnector)
    {
        _backendConnector = backendConnector;
    }

    public string UserAgent
    {
        get => _config.UserAgent;
        set => Set(_config.UserAgent, value, v => _config.UserAgent = v, v => new Config { UserAgent = v });
    }

    public string SavePath
    {
        get => _config.SavePath;
        set => Set(_config.SavePath, value, v => _config.SavePath = v, v => new Config { SavePath = v });
    }

    public FilenameScheme FilenameScheme
    {
        get => _config.FilenameScheme;
        set => Set(_config.FilenameScheme, value, v => _config.FilenameScheme = v,
            v => new Config { FilenameScheme = v });
    }

    public UnzipProtocol UnzipProtocol
    {
        get => _config.UnzipProtocol;
        set => Set(_config.UnzipProtocol, value, v => _config.UnzipProtocol = v,
            v => new Config { UnzipProtocol = v });
    }

    // PostDownloadAction is patched as a whole object, so send the full current value
    public bool RemoveDuplicates
    {
        get => _config.PostDownloadAction.FileLevel.RemoveDuplicates;
        set => Set(RemoveDuplicates, value, v => _config.PostDownloadAction.FileLevel.RemoveDuplicates = v,
            _ => new Config { PostDownloadAction = _config.PostDownloadAction });
    }

    public bool RunPostProcessScript
    {
        get => _config.PostDownloadAction.DirectoryLevel.RunPostProcessScript;
        set => Set(RunPostProcessScript, value,
            v => _config.PostDownloadAction.DirectoryLevel.RunPostProcessScript = v,
            _ => new Config { PostDownloadAction = _config.PostDownloadAction });
    }

    public int MaxRetries
    {
        get => _config.MaxRetries;
        set => Set(_config.MaxRetries, value, v => _config.MaxRetries = v, v => new Config { MaxRetries = v });
    }

    public int RetryDelay
    {
        get => _config.RetryDelay;
        set => Set(_config.RetryDelay, value, v => _config.RetryDelay = v, v => new Config { RetryDelay = v });
    }

    public bool SkipFailedDownloads
    {
        get => _config.SkipFailedDownloads;
        set => Set(_config.SkipFailedDownloads, value, v => _config.SkipFailedDownloads = v,
            v => new Config { SkipFailedDownloads = v });
    }

    public bool SaveUnfinishedUrls
    {
        get => _config.SaveUnfinishedUrls;
        set => Set(_config.SaveUnfinishedUrls, value, v => _config.SaveUnfinishedUrls = v,
            v => new Config { SaveUnfinishedUrls = v });
    }

    public string FlareSolverrUri
    {
        get => _config.FlareSolverrUri;
        set => Set(_config.FlareSolverrUri, value, v => _config.FlareSolverrUri = v,
            v => new Config { FlareSolverrUri = v });
    }

    public bool CloseFlareSolverrSession
    {
        get => _config.CloseFlareSolverrSession;
        set => Set(_config.CloseFlareSolverrSession, value, v => _config.CloseFlareSolverrSession = v,
            v => new Config { CloseFlareSolverrSession = v });
    }

    public string CSWebDriverUri
    {
        get => _config.CSWebDriverUri;
        set => Set(_config.CSWebDriverUri, value, v => _config.CSWebDriverUri = v,
            v => new Config { CSWebDriverUri = v });
    }

    public IReadOnlyDictionary<string, SiteConfig> Sites => _config.Sites;

    public void SetSites(IReadOnlyDictionary<string, SiteConfig> sites)
    {
        // Null values tell the backend to remove that site
        var patch = new Dictionary<string, SiteConfig?>(StringComparer.OrdinalIgnoreCase);
        foreach (var removed in _config.Sites.Keys.Where(name => !sites.ContainsKey(name)))
        {
            patch[removed] = null;
        }

        foreach (var (name, site) in sites)
        {
            patch[name] = site;
        }

        _config.Sites = new Dictionary<string, SiteConfig>(sites, StringComparer.OrdinalIgnoreCase);
        UpdateRemoteConfig(new Config { Sites = patch });
    }

    public async Task<IReadOnlyList<string>> GetKnownSiteNames(CancellationToken cancellationToken = default)
    {
        if (!_initialized)
        {
            return _config.Sites.Keys.ToList();
        }

        return await _backendConnector.GetSiteNamesAsync(cancellationToken);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        _loading = true;

        try
        {
            _config = await _backendConnector.GetConfigAsync(cancellationToken);
            _initialized = true;
        }
        finally
        {
            _loading = false;
        }
    }

    private void Set<T>(T current, T value, Action<T> apply, Func<T, Config> createPatch)
    {
        if (EqualityComparer<T>.Default.Equals(current, value))
        {
            return;
        }

        apply(value);
        UpdateRemoteConfig(createPatch(value));
    }

    private void UpdateRemoteConfig(Config configPatch)
    {
        if (!_initialized || _loading)
        {
            return;
        }

        _ = UpdateRemoteConfigAsync(configPatch);
    }

    private async Task UpdateRemoteConfigAsync(Config configPatch, CancellationToken cancellationToken = default)
    {
        try
        {
            await _backendConnector.UpdateConfigAsync(configPatch, cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }
}
