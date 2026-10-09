using System.Text.Json;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Sdk.Configuration;
using NicheImageRipper.Sdk.Enums;

namespace NicheImageRipper.Service.Models.Configs;

public class Config
{
    public string? UserAgent { get; set; }
    public string? SavePath { get; set; }
    public string? Theme { get; set; }
    public FilenameScheme? FilenameScheme { get; set; }
    public UnzipProtocol? UnzipProtocol { get; set; }
    public PostDownloadAction? PostDownloadAction { get; set; }
    public bool? AskToReRip { get; set; }
    public bool? LiveHistory { get; set; }
    public bool? SkipFailedDownloads { get; set; }
    public int? NumThreads { get; set; }
    public int? MaxRetries { get; set; }
    public int? RetryDelay { get; set; }
    public string? FlareSolverrUri { get; set; }
    public bool? CloseFlareSolverrSession { get; set; }
    public string? CSWebDriverUri { get; set; }
    public Dictionary<string, SiteConfig>? Sites { get; set; }

    public static Config FromCoreConfig(GeneralConfig generalConfig)
    {
        var config = new Config
        {
            UserAgent = generalConfig.UserAgent,
            SavePath = generalConfig.SavePath,
            Theme = generalConfig.Theme,
            FilenameScheme = generalConfig.FilenameScheme,
            UnzipProtocol = generalConfig.UnzipProtocol,
            PostDownloadAction = generalConfig.PostDownloadAction,
            AskToReRip = generalConfig.AskToReRip,
            LiveHistory = generalConfig.LiveHistory,
            SkipFailedDownloads = generalConfig.SkipFailedDownloads,
            NumThreads = generalConfig.NumThreads,
            MaxRetries = generalConfig.MaxRetries,
            RetryDelay = generalConfig.RetryDelay,
            FlareSolverrUri = generalConfig.FlareSolverrUri,
            CloseFlareSolverrSession = generalConfig.CloseFlareSolverrSession,
            CSWebDriverUri = generalConfig.CSWebDriverUri,
            Sites = generalConfig.Sites,
        };
        return config;
    }
}