using System.Text.Json;
using System.Text.Json.Serialization;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.Utility;

namespace NicheImageRipper.Sdk.Configuration;

public class GeneralConfig
{
    public string UserAgent { get; set; } =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/134.0.0.0 Safari/537.36";

    public string SavePath { get; set; } = "./Rips/";
    public string Theme { get; set; } = "Dark";
    public FilenameScheme FilenameScheme { get; set; } = FilenameScheme.Original;
    public UnzipProtocol UnzipProtocol { get; set; } = UnzipProtocol.None;
    public PostDownloadAction PostDownloadAction { get; set; }
    public bool AskToReRip { get; set; } = true;
    public bool LiveHistory { get; set; } = false;
    public bool SkipFailedDownloads { get; set; } = true;
    public int NumThreads { get; set; } = 1;
    public int MaxRetries { get; set; } = 4;
    public int RetryDelay { get; set; } = 1000;
    public string FlareSolverrUri { get; set; } = "";
    public bool CloseFlareSolverrSession { get; set; } = true;
    public string CSWebDriverUri { get; set; } = "";
    public bool SaveUnfinishedUrls { get; set; } = true;

    /// <summary>Per-site settings keyed by IHtmlParser.ParserName.</summary>
    public Dictionary<string, SiteConfig> Sites
    {
        get;
        // System.Text.Json builds a fresh dictionary on load and ignores the initializer's comparer, so re-wrap
        set => field = new Dictionary<string, SiteConfig>(value, StringComparer.OrdinalIgnoreCase);
    } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Login credentials keyed by IHtmlParser.ParserName.</summary>
    [Obsolete("Migrated into Sites; kept only to read old config files.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, Credentials>? Logins { get; set; }

    /// <summary>API keys/tokens keyed by IHtmlParser.ParserName.</summary>
    [Obsolete("Migrated into Sites; kept only to read old config files.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Keys { get; set; }

    /// <summary>Cookie values keyed by IHtmlParser.ParserName. Always an array (single-value sites use a
    /// one-element array).</summary>
    [Obsolete("Migrated into Sites; kept only to read old config files.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string[]>? Cookies { get; set; }

    /// <summary>Arbitrary per-site config keyed by IHtmlParser.ParserName. Each parser owns and deserializes
    /// its own shape from the JsonElement.</summary>
    [Obsolete("Migrated into Sites; kept only to read old config files.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, JsonElement>? Custom { get; set; }

    [Obsolete("Migrated into Sites; kept only to read old config files.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, SettingsOverride>? ParserSpecificSettingsOverrides { get; set; }

#pragma warning disable CS0618
    /// <returns>true if anything was migrated and the config should be re-saved</returns>
    public bool MigrateLegacy()
    {
        var changed = false;
        foreach (var (name, v) in Logins ?? [])
        {
            GetOrAddSiteConfig(name).Login = v;
            changed = true;
        }

        foreach (var (name, v) in Keys ?? [])
        {
            GetOrAddSiteConfig(name).Key = v;
            changed = true;
        }

        foreach (var (name, v) in Cookies ?? [])
        {
            GetOrAddSiteConfig(name).Cookies = v;
            changed = true;
        }

        foreach (var (name, v) in Custom ?? [])
        {
            GetOrAddSiteConfig(name).Custom = v;
            changed = true;
        }

        foreach (var (name, v) in ParserSpecificSettingsOverrides ?? [])
        {
            GetOrAddSiteConfig(name).Overrides = v;
            changed = true;
        }

        Logins = null;
        Keys = null;
        Cookies = null;
        Custom = null;
        ParserSpecificSettingsOverrides = null;
        return changed;
    }
#pragma warning restore CS0618

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtraData { get; set; } = new();

    public void SaveConfig()
    {
        JsonUtility.Serialize(Config.ConfigPath, this);
    }

    public SiteConfig? GetSiteConfig(string parserName) => Sites.GetValueOrDefault(parserName);

    public SiteConfig GetOrAddSiteConfig(string parserName)
    {
        if (!Sites.TryGetValue(parserName, out var site))
        {
            Sites[parserName] = site = new SiteConfig();
        }

        return site;
    }
}