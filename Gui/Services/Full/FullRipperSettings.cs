using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Gui.Models;
using NicheImageRipper.Sdk.Configuration;
using NicheImageRipper.Sdk.Enums;

namespace NicheImageRipper.Gui.Services.Full;

public class FullRipperSettings : IRipperSettings
{
    private static GuiConfig Config => (GuiConfig) Sdk.Configuration.Config.Instance;

    public string UserAgent
    {
        get => Config.UserAgent;
        set => Config.UserAgent = value;
    }

    public string SavePath
    {
        get => Config.SavePath;
        set => Config.SavePath = value;
    }

    public FilenameScheme FilenameScheme
    {
        get => Config.FilenameScheme;
        set => Config.FilenameScheme = value;
    }

    public UnzipProtocol UnzipProtocol
    {
        get => Config.UnzipProtocol;
        set => Config.UnzipProtocol = value;
    }

    public bool RemoveDuplicates
    {
        get => Config.PostDownloadAction.FileLevel.RemoveDuplicates;
        set => Config.PostDownloadAction.FileLevel.RemoveDuplicates = value;
    }

    public bool RunPostProcessScript
    {
        get => Config.PostDownloadAction.DirectoryLevel.RunPostProcessScript;
        set => Config.PostDownloadAction.DirectoryLevel.RunPostProcessScript = value;
    }

    public int MaxRetries
    {
        get => Config.MaxRetries;
        set => Config.MaxRetries = value;
    }

    public int RetryDelay
    {
        get => Config.RetryDelay;
        set => Config.RetryDelay = value;
    }

    public bool SkipFailedDownloads
    {
        get => Config.SkipFailedDownloads;
        set => Config.SkipFailedDownloads = value;
    }

    public bool SaveUnfinishedUrls
    {
        get => Config.SaveUnfinishedUrls;
        set => Config.SaveUnfinishedUrls = value;
    }

    public string FlareSolverrUri
    {
        get => Config.FlareSolverrUri;
        set => Config.FlareSolverrUri = value;
    }

    public bool CloseFlareSolverrSession
    {
        get => Config.CloseFlareSolverrSession;
        set => Config.CloseFlareSolverrSession = value;
    }

    public string CSWebDriverUri
    {
        get => Config.CSWebDriverUri;
        set => Config.CSWebDriverUri = value;
    }

    public IReadOnlyDictionary<string, SiteConfig> Sites => Config.Sites;

    public void SetSites(IReadOnlyDictionary<string, SiteConfig> sites)
    {
        // The setter re-wraps with a case-insensitive comparer
        Config.Sites = new Dictionary<string, SiteConfig>(sites, StringComparer.OrdinalIgnoreCase);
    }

    public Task<IReadOnlyList<string>> GetKnownSiteNames(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<string>>(Core.NicheImageRipper.GetParserNames());
    }
}
