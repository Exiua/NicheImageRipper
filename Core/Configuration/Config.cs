using Core.Enums;
using Core.Utility;
using JetBrains.Annotations;

namespace Core.Configuration;

public class Config
{
    private const string ConfigPath = "config.json";
    
    private static Config? _config;

    public static string UserAgent =>
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Safari/537.36";

    public static Config Instance => _config ??= LoadConfig();

    public string SavePath { get; set; } = null!;
    public string Theme { get; set; } = null!;
    public FilenameScheme FilenameScheme { get; set; }
    public UnzipProtocol UnzipProtocol { get; set; }
    public bool AskToReRip { get; set; }
    public bool LiveHistory { get; set; }
    public int NumThreads { get; set; }
    public Dictionary<string, Credentials> Logins { get; set; } = null!;
    public Dictionary<string, string> Keys { get; set; } = null!;
    public Dictionary<string, string> Cookies { get; set; } = null!;

    // Used for deserialization
    [UsedImplicitly]
    public Config()
    {
        
    }
    
    private static Config CreateTemplateConfig()
    {
        var config = new Config
        {
            SavePath = "./Rips/",
            Theme = "Dark",
            FilenameScheme = FilenameScheme.Original,
            UnzipProtocol = UnzipProtocol.None,
            AskToReRip = true,
            LiveHistory = false,
            NumThreads = 1,
            Logins = new Dictionary<string, Credentials>()
        };
        
        string[] siteLogins = ["Sexy-Egirls", "V2Ph", "Porn3dx", "DeviantArt", "Mega", "TitsInTops", "Newgrounds", "Nijie", "SimpCity"];
        foreach (var site in siteLogins)
        {
            config.Logins[site] = new Credentials();
        }

        config.Keys = new Dictionary<string, string>();
        string[] siteKeys = ["Imgur", "Google", "Dropbox", "Pixeldrain"];
        foreach (var site in siteKeys)
        {
            config.Keys[site] = "";
        }
        
        config.Cookies = new Dictionary<string, string>();
        string[] siteCookies = ["Twitter", "Newgrounds"];
        foreach (var site in siteCookies)
        {
            config.Cookies[site] = "";
        }

        return config;
    }

    public void SaveConfig()
    {
        JsonUtility.Serialize(ConfigPath, this);
    }
    
    private static Config LoadConfig()
    {
        return File.Exists(ConfigPath)
            ? JsonUtility.Deserialize<Config>(ConfigPath)!
            : CreateTemplateConfig();
    }
}