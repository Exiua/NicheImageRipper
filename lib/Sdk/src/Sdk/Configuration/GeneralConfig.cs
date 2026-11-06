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
    public PostDownloadAction PostDownloadAction { get; set; } = new();
    public ReRipBehavior ReRipBehavior { get; set; } = ReRipBehavior.Ask;
    public GalleryUpdateMode GalleryUpdateMode { get; set; } = GalleryUpdateMode.UpdatesOnly;
    public SubParserOverrideMode SubParserOverrides { get; set; } = SubParserOverrideMode.Own;
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

    /// <summary>The site's <see cref="SettingsOverride.GalleryUpdateMode"/> if set, otherwise <see cref="GalleryUpdateMode"/>.</summary>
    public GalleryUpdateMode GetGalleryUpdateMode(string parserName) =>
        GetSiteConfig(parserName)?.Overrides?.GalleryUpdateMode ?? GalleryUpdateMode;

    /// <summary>The site's <see cref="SettingsOverride.GalleryUpdateStrategy"/>; null when it uses the parser's default.</summary>
    public GalleryUpdateStrategy? GetGalleryUpdateStrategy(string parserName) =>
        GetSiteConfig(parserName)?.Overrides?.GalleryUpdateStrategy;

    /// <summary>The filename scheme for a parser another parser delegated to, per <see cref="SubParserOverrides"/>.</summary>
    /// <param name="parserName">The callee's parser name.</param>
    /// <param name="callerScheme">The filename scheme the caller is using.</param>
    public FilenameScheme GetSubParserFilenameScheme(string parserName, FilenameScheme callerScheme) =>
        SubParserOverrides switch
        {
            SubParserOverrideMode.Inherit => callerScheme,
            SubParserOverrideMode.None => FilenameScheme,
            _ => GetSiteConfig(parserName)?.Overrides is { } overrides && Enum.IsDefined(overrides.FilenameScheme)
                ? overrides.FilenameScheme
                : callerScheme,
        };

    /// <summary>The gallery update mode for a parser another parser delegated to, per <see cref="SubParserOverrides"/>.</summary>
    /// <param name="parserName">The callee's parser name.</param>
    /// <param name="callerMode">The gallery update mode the caller is using.</param>
    public GalleryUpdateMode GetSubParserGalleryUpdateMode(string parserName, GalleryUpdateMode callerMode) =>
        SubParserOverrides switch
        {
            SubParserOverrideMode.Inherit => callerMode,
            SubParserOverrideMode.None => GalleryUpdateMode,
            _ => GetSiteConfig(parserName)?.Overrides?.GalleryUpdateMode ?? callerMode,
        };

    /// <summary>
    ///     The update strategy for a parser another parser delegated to. Strategies are parser-specific, so this is
    ///     never inherited: it's the callee's own unless <see cref="SubParserOverrides"/> ignores overrides.
    /// </summary>
    public GalleryUpdateStrategy? GetSubParserGalleryUpdateStrategy(string parserName) =>
        SubParserOverrides == SubParserOverrideMode.None ? null : GetGalleryUpdateStrategy(parserName);

    public SiteConfig GetOrAddSiteConfig(string parserName)
    {
        if (!Sites.TryGetValue(parserName, out var site))
        {
            Sites[parserName] = site = new SiteConfig();
        }

        return site;
    }
}