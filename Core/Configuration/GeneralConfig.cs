using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Enums;
using Core.Utility;
using JetBrains.Annotations;

namespace Core.Configuration;

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
    public LoginConfig Logins { get; set; }
    public KeyConfig Keys { get; set; }
    public CookieConfig Cookies { get; set; }
    public CustomConfig Custom { get; set; }
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
        Logins = LoginConfig.New();
        Keys = KeyConfig.New();
        Cookies = CookieConfig.New();
        Custom = CustomConfig.New();
        ExtraData = new Dictionary<string, JsonElement>();
        ParserSpecificSettingsOverrides = new Dictionary<string, SettingsOverride>();
    }
    
    public void SaveConfig()
    {
        JsonUtility.Serialize(Config.ConfigPath, this);
    }
}