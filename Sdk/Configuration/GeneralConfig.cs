using System.Text.Json;
using System.Text.Json.Serialization;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.Utility;

namespace NicheImageRipper.Sdk.Configuration;

public class GeneralConfig
{
    public string UserAgent { get; set; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/134.0.0.0 Safari/537.36";
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

    /// <summary>Login credentials keyed by IHtmlParser.ParserName.</summary>
    public Dictionary<string, Credentials> Logins { get; set; } = new();

    /// <summary>API keys/tokens keyed by IHtmlParser.ParserName.</summary>
    public Dictionary<string, string> Keys { get; set; } = new();

    /// <summary>Cookie values keyed by IHtmlParser.ParserName. Always an array (single-value sites use a
    /// one-element array).</summary>
    public Dictionary<string, string[]> Cookies { get; set; } = new();

    /// <summary>Arbitrary per-site config keyed by IHtmlParser.ParserName. Each parser owns and deserializes
    /// its own shape from the JsonElement.</summary>
    public Dictionary<string, JsonElement> Custom { get; set; } = new();

    public Dictionary<string, SettingsOverride> ParserSpecificSettingsOverrides { get; set; } = new();

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtraData { get; set; } = new();

    public void SaveConfig()
    {
        JsonUtility.Serialize(Config.ConfigPath, this);
    }
}