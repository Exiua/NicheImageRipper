using NicheImageRipper.Core.Configuration;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.History;
using NicheImageRipper.Service.Utilities.ExtensionMethods;
using Config = NicheImageRipper.Service.Models.Configs.Config;
using SettingsOverride = NicheImageRipper.Core.Configuration.SettingsOverride;

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
            return  _ripper.QueueUrls(urls);
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
        return NicheImageRipper.Core.NicheImageRipper.Config;
    }

    public void UpdateConfig(Config config)
    {
        var existingConfig = NicheImageRipper.Core.NicheImageRipper.Config;
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
            var logins = config.Logins;
            var existingLogins = existingConfig.Logins;
            existingLogins.DeviantArt.UpdateCredentials(logins.DeviantArt);
            existingLogins.Mega.UpdateCredentials(logins.Mega);
            existingLogins.TitsInTops.UpdateCredentials(logins.TitsInTops);
            existingLogins.Nijie.UpdateCredentials(logins.Nijie);
            existingLogins.Danbooru.UpdateCredentials(logins.Danbooru);
            existingLogins.Gelbooru.UpdateCredentials(logins.Gelbooru);
            existingLogins.Rule34.UpdateCredentials(logins.Rule34);
            existingLogins.Yandere.UpdateCredentials(logins.Yandere);
            existingLogins.E621.UpdateCredentials(logins.E621);
            existingLogins.EHentai.UpdateCredentials(logins.EHentai);
            existingLogins.SteamCommunity.UpdateCredentials(logins.SteamCommunity);
        }

        if (config.Keys is not null)
        {
            var keys = config.Keys;
            var existingKeys = existingConfig.Keys;
            if (keys.Imgur is not null)
            {
                existingKeys.Imgur = keys.Imgur;
            }
            if (keys.Google is not null)
            {
                existingKeys.Google = keys.Google;
            }
            if (keys.Dropbox is not null)
            {
                existingKeys.Dropbox = keys.Dropbox;
            }
            if (keys.Pixeldrain is not null)
            {
                existingKeys.Pixeldrain = keys.Pixeldrain;
            }
            if (keys.Pixiv is not null)
            {
                existingKeys.Pixiv = keys.Pixiv;
            }
        }

        if (config.Cookies is not null)
        {
            var cookies = config.Cookies;
            var existingCookies = existingConfig.Cookies;
            if (cookies.Twitter is not null)
            {
                existingCookies.Twitter = cookies.Twitter;
            }
            if (cookies.Newgrounds is not null)
            {
                existingCookies.Newgrounds = cookies.Newgrounds;
            }
            if (cookies.Porn3dx is not null)
            {
                existingCookies.Porn3dx = cookies.Porn3dx;
            }
            if (cookies.Pornhub is not null)
            {
                existingCookies.Pornhub = cookies.Pornhub;
            }
            if (cookies.Thothub is not null)
            {
                existingCookies.Thothub = cookies.Thothub;
            }
            if (cookies.Kemono is not null)
            {
                existingCookies.Kemono = cookies.Kemono;
            }
            if (cookies.SimpCity is not null)
            {
                existingCookies.SimpCity = cookies.SimpCity;
            }
            if (cookies.Pixiv is not null)
            {
                existingCookies.Pixiv = cookies.Pixiv;
            }
            if (cookies.SteamCommunity is not null)
            {
                existingCookies.SteamCommunity = cookies.SteamCommunity;
            }
        }

        if (config.Custom is not null)
        {
            var custom = config.Custom;
            var existingCustom = existingConfig.Custom;
            if (custom.V2PH is not null)
            {
                var v2ph = custom.V2PH;
                if (v2ph.Frontend is not null)
                {
                    existingCustom.V2PH.Frontend = v2ph.Frontend;
                }

                if (v2ph.FrontendRmt is not null)
                {
                    existingCustom.V2PH.FrontendRmt = v2ph.FrontendRmt;
                }

                if (v2ph.CfClearance is not null)
                {
                    existingCustom.V2PH.CfClearance = v2ph.CfClearance;
                }
            }
            
            if (custom.GoFile is not null)
            {
                var goFile = custom.GoFile;
                if (goFile.AccountToken is not null)
                {
                    existingCustom.GoFile.AccountToken = goFile.AccountToken;
                }

                if (goFile.LoginLink is not null)
                {
                    existingCustom.GoFile.LoginLink = goFile.LoginLink;
                }
            }
        }

        if (config.ParserSpecificSettingsOverrides is not null)
        {
            foreach (var (key, value) in config.ParserSpecificSettingsOverrides)
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
        return _ripper.SaveData();
    }

    public void LoadUrls(List<string> urls)
    {
        _ripper.LoadUrls(urls);
    }
}