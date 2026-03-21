using Core;
using Core.DataStructures;
using Core.History;
using CoreService.Models.Configs;
using CoreService.Models.Dtos;
using CoreService.Utilities.ExtensionMethods;
using SettingsOverride = Core.Configuration.SettingsOverride;

namespace CoreService.Singletons;

public class NicheImageRipperSingleton(ILogger<NicheImageRipperSingleton> logger) : INicheImageRipperSingleton
{
    private readonly NicheImageRipper _ripper = new();
    private readonly Lock _ripperLock = new();
    private bool _isRipping;
    
    public string[] GetQueueSnapshot()
    {
        using (_ripperLock.EnterScope())
        {
            return _ripper.UrlQueue.Select(u => u).ToArray();
        }
    }

    public IEnumerable<RejectedUrlInfoDto> Queue(string[] urls)
    {
        using (_ripperLock.EnterScope())
        {
            var rejected = urls.Select(url => _ripper.QueueUrls(url))
                               .Where(rejectedUrlsInfo => rejectedUrlsInfo.HasRejectedUrls)
                               .SelectMany(rejectedUrlsInfo => rejectedUrlsInfo.Urls)
                               .Select(rejectedUrl => new RejectedUrlInfoDto(rejectedUrl.Url, rejectedUrl.Reason))
                               .ToList();
            return rejected;
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
                logger.LogError(e, "An error occurred while ripping.");
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
        return NicheImageRipper.GetHistoryPage(start, offset, filter);
    }

    public int GetHistoryCount()
    {
        return NicheImageRipper.GetHistoryCount();
    }

    public Config GetConfig()
    {
        return Config.FromCoreConfig(NicheImageRipper.Config);
    }

    public void UpdateConfig(Config config)
    {
        if (config.UserAgent is not null)
        {
            NicheImageRipper.Config.UserAgent = config.UserAgent;
        }

        if (config.SavePath is not null)
        {
            NicheImageRipper.Config.SavePath = config.SavePath;
        }
        
        if (config.Theme is not null)
        {
            NicheImageRipper.Config.Theme = config.Theme;
        }
        
        if (config.FilenameScheme is not null)
        {
            NicheImageRipper.Config.FilenameScheme = config.FilenameScheme.Value;
        }
        
        if (config.UnzipProtocol is not null)
        {
            NicheImageRipper.Config.UnzipProtocol = config.UnzipProtocol.Value;
        }
        
        if (config.PostDownloadAction is not null)
        {
            NicheImageRipper.Config.PostDownloadAction = config.PostDownloadAction.Value;
        }
        
        if (config.AskToReRip is not null)
        {
            NicheImageRipper.Config.AskToReRip = config.AskToReRip.Value;
        }
        
        if (config.LiveHistory is not null)
        {
            NicheImageRipper.Config.LiveHistory = config.LiveHistory.Value;
        }
        
        if (config.SkipFailedDownloads is not null)
        {
            NicheImageRipper.Config.SkipFailedDownloads = config.SkipFailedDownloads.Value;
        }
        
        if (config.NumThreads is not null)
        {
            NicheImageRipper.Config.NumThreads = config.NumThreads.Value;
        }
        
        if (config.MaxRetries is not null)
        {
            NicheImageRipper.Config.MaxRetries = config.MaxRetries.Value;
        }
        
        if (config.RetryDelay is not null)
        {
            NicheImageRipper.Config.RetryDelay = config.RetryDelay.Value;
        }
        
        if (config.FlareSolverrUri is not null)
        {
            NicheImageRipper.Config.FlareSolverrUri = config.FlareSolverrUri;
        }
        
        if (config.CloseFlareSolverrSession is not null)
        {
            NicheImageRipper.Config.CloseFlareSolverrSession = config.CloseFlareSolverrSession.Value;
        }
        
        if (config.CSWebDriverUri is not null)
        {
            NicheImageRipper.Config.CSWebDriverUri = config.CSWebDriverUri;
        }
        
        if (config.Logins is not null)
        {
            var logins = config.Logins;
            NicheImageRipper.Config.Logins.DeviantArt.UpdateCredentials(logins.DeviantArt);
            NicheImageRipper.Config.Logins.Mega.UpdateCredentials(logins.Mega);
            NicheImageRipper.Config.Logins.TitsInTops.UpdateCredentials(logins.TitsInTops);
            NicheImageRipper.Config.Logins.Nijie.UpdateCredentials(logins.Nijie);
            NicheImageRipper.Config.Logins.Danbooru.UpdateCredentials(logins.Danbooru);
            NicheImageRipper.Config.Logins.Gelbooru.UpdateCredentials(logins.Gelbooru);
            NicheImageRipper.Config.Logins.Rule34.UpdateCredentials(logins.Rule34);
            NicheImageRipper.Config.Logins.Yandere.UpdateCredentials(logins.Yandere);
            NicheImageRipper.Config.Logins.E621.UpdateCredentials(logins.E621);
            NicheImageRipper.Config.Logins.EHentai.UpdateCredentials(logins.EHentai);
        }

        if (config.Keys is not null)
        {
            var keys  = config.Keys;
            if (keys.Imgur is not null)
            {
                NicheImageRipper.Config.Keys.Imgur = keys.Imgur;
            }
            if (keys.Google is not null)
            {
                NicheImageRipper.Config.Keys.Google = keys.Google;
            }
            if (keys.Dropbox is not null)
            {
                NicheImageRipper.Config.Keys.Dropbox = keys.Dropbox;
            }
            if (keys.Pixeldrain is not null)
            {
                NicheImageRipper.Config.Keys.Pixeldrain = keys.Pixeldrain;
            }
            if (keys.Pixiv is not null)
            {
                NicheImageRipper.Config.Keys.Pixiv = keys.Pixiv;
            }
        }

        if (config.Cookies is not null)
        {
            var cookies = config.Cookies;
            if (cookies.Twitter is not null)
            {
                NicheImageRipper.Config.Cookies.Twitter = cookies.Twitter;
            }
            if (cookies.Newgrounds is not null)
            {
                NicheImageRipper.Config.Cookies.Newgrounds = cookies.Newgrounds;
            }
            if (cookies.Porn3dx is not null)
            {
                NicheImageRipper.Config.Cookies.Porn3dx = cookies.Porn3dx;
            }
            if (cookies.Pornhub is not null)
            {
                NicheImageRipper.Config.Cookies.Pornhub = cookies.Pornhub;
            }
            if (cookies.Thothub is not null)
            {
                NicheImageRipper.Config.Cookies.Thothub = cookies.Thothub;
            }
            if (cookies.Kemono is not null)
            {
                NicheImageRipper.Config.Cookies.Kemono = cookies.Kemono;
            }
            if (cookies.SimpCity is not null)
            {
                NicheImageRipper.Config.Cookies.SimpCity = cookies.SimpCity;
            }
            if (cookies.Pixiv is not null)
            {
                NicheImageRipper.Config.Cookies.Pixiv = cookies.Pixiv;
            }
            if (cookies.SteamCommunity is not null)
            {
                NicheImageRipper.Config.Cookies.SteamCommunity = cookies.SteamCommunity;
            }
        }

        if (config.Custom is not null)
        {
            var custom = config.Custom;
            if (custom.V2PH is not null)
            {
                var v2ph = custom.V2PH;
                if (v2ph.Frontend is not null)
                {
                    NicheImageRipper.Config.Custom.V2PH.Frontend = v2ph.Frontend;
                }

                if (v2ph.FrontendRmt is not null)
                {
                    NicheImageRipper.Config.Custom.V2PH.FrontendRmt = v2ph.FrontendRmt;
                }

                if (v2ph.CfClearance is not null)
                {
                    NicheImageRipper.Config.Custom.V2PH.CfClearance = v2ph.CfClearance;
                }
            }
            
            if (custom.GoFile is not null)
            {
                var goFile = custom.GoFile;
                if (goFile.AccountToken is not null)
                {
                    NicheImageRipper.Config.Custom.GoFile.AccountToken = goFile.AccountToken;
                }

                if (goFile.LoginLink is not null)
                {
                    NicheImageRipper.Config.Custom.GoFile.LoginLink = goFile.LoginLink;
                }
            }
            
            if (custom.SteamCommunity is not null)
            {
                var steamCommunity = custom.SteamCommunity;
                if (steamCommunity.Username is not null)
                {
                    NicheImageRipper.Config.Custom.SteamCommunity.Username = steamCommunity.Username;
                }
            }
        }

        if (config.ParserSpecificSettingsOverrides is not null)
        {
            foreach (var (key, value) in config.ParserSpecificSettingsOverrides)
            {
                SettingsOverride settingsOverride;
                if (NicheImageRipper.Config.ParserSpecificSettingsOverrides.TryGetValue(key, out var @override))
                {
                    settingsOverride = @override;
                }
                else
                {
                    settingsOverride = new SettingsOverride();
                    NicheImageRipper.Config.ParserSpecificSettingsOverrides[key] = settingsOverride;
                }
                
                if (value.FilenameScheme is not null)
                {
                    settingsOverride.FilenameScheme = value.FilenameScheme.Value;
                }
            }
        }
    }
}