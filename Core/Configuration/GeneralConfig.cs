using System.Text.Json;
using System.Text.Json.Serialization;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Utility;

namespace NicheImageRipper.Core.Configuration;

public class GeneralConfig
{
    public string UserAgent { get; set; }
    public string SavePath { get; set; }
    public string Theme { get; set; }
    public FilenameScheme FilenameScheme { get; set; }
    public UnzipProtocol UnzipProtocol { get; set; }
    public PostDownloadAction PostDownloadAction { get; set; }
    public bool AskToReRip { get; set; }
    public bool LiveHistory { get; set; }
    public bool SkipFailedDownloads { get; set; }
    public int NumThreads { get; set; }
    public int MaxRetries { get; set; }
    public int RetryDelay { get; set; }
    public string FlareSolverrUri { get; set; }
    public bool CloseFlareSolverrSession { get; set; }
    public string CSWebDriverUri { get; set; }

    /// <summary>Login credentials keyed by IHtmlParser.ParserName.</summary>
    public Dictionary<string, Credentials> Logins { get; set; }

    /// <summary>API keys/tokens keyed by IHtmlParser.ParserName.</summary>
    public Dictionary<string, string> Keys { get; set; }

    /// <summary>Cookie values keyed by IHtmlParser.ParserName. Always an array (single-value sites use a
    /// one-element array).</summary>
    public Dictionary<string, string[]> Cookies { get; set; }

    /// <summary>Arbitrary per-site config keyed by IHtmlParser.ParserName. Each parser owns and deserializes
    /// its own shape from the JsonElement.</summary>
    public Dictionary<string, JsonElement> Custom { get; set; }

    public Dictionary<string, SettingsOverride> ParserSpecificSettingsOverrides { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtraData { get; set; }

    public GeneralConfig()
    {
        UserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/134.0.0.0 Safari/537.36";
        SavePath = "./Rips/";
        Theme = "Dark";
        FilenameScheme = FilenameScheme.Original;
        UnzipProtocol = UnzipProtocol.None;
        AskToReRip = true;
        LiveHistory = false;
        SkipFailedDownloads = true;
        NumThreads = 1;
        MaxRetries = 4;
        RetryDelay = 1000;
        FlareSolverrUri = "";
        CloseFlareSolverrSession = true;
        CSWebDriverUri = "";
        Logins = new Dictionary<string, Credentials>();
        Keys = new Dictionary<string, string>();
        Cookies = new Dictionary<string, string[]>();
        Custom = new Dictionary<string, JsonElement>();
        ExtraData = new Dictionary<string, JsonElement>();
        ParserSpecificSettingsOverrides = new Dictionary<string, SettingsOverride>();
    }

    public void SaveConfig()
    {
        JsonUtility.Serialize(Config.ConfigPath, this);
    }
}