using Core.Enums;
using JetBrains.Annotations;

namespace Core.Configuration;

public class Config
{
    private const string ConfigPath = "config.json";
    
    private static Config? _config;
    
    public static Config Instance
    {
        get
        {
            if (_config is null)
            {
                _config = LoadConfig();
            }
            return _config;
        }
    }

    public string SavePath { get; set; } = null!;
    public string Theme { get; set; } = null!;
    public FilenameScheme FilenameScheme { get; set; }
    public UnzipProtocol UnzipProtocol { get; set; }
    public bool AskToReRip { get; set; }
    public bool LiveHistory { get; set; }
    public int NumThreads { get; set; }
    public Dictionary<string, Credentials> Logins { get; private set; } = null!;
    public Dictionary<string, string> Keys { get; private set; } = null!;

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

        return config;
    }

    public void SaveConfig()
    {
        JsonUtility.Serialize(ConfigPath, this);
    }
    
    private static Config LoadConfig()
    {
        return File.Exists(ConfigPath)
            ? JsonUtility.Deserialize<Config>(ConfigPath)
            : CreateTemplateConfig();
    }
}