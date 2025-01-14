using Core.Enums;
using Core.Utility;
using JetBrains.Annotations;

namespace Core.Configuration;

public class Config
{
    private const string ConfigPath = "config.json";

    private static Config? _config;

    public static string UserAgent =>
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36";

    public static Config Instance => _config ??= LoadConfig();

    public string SavePath { get; set; } = null!;
    public string Theme { get; set; } = null!;
    public FilenameScheme FilenameScheme { get; set; }
    public UnzipProtocol UnzipProtocol { get; set; }
    public bool AskToReRip { get; set; }
    public bool LiveHistory { get; set; }
    public int NumThreads { get; set; }
    public string FlareSolverrUri { get; set; }
    public bool CloseFlareSolverrSession { get; set; }
    public Dictionary<string, Credentials> Logins { get; set; } = null!;
    public Dictionary<string, string> Keys { get; set; } = null!;
    public Dictionary<string, string> Cookies { get; set; } = null!;
    public Dictionary<string, Dictionary<string, string>> Custom { get; set; } = null!;

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
            FlareSolverrUri = "http://localhost:8191/v1",
            Logins = new Dictionary<string, Credentials>()
        };

        string[] siteLogins = [
            ConfigKeys.LoginKeys.SexyEGirls,
            ConfigKeys.LoginKeys.DeviantArt,
            ConfigKeys.LoginKeys.Mega,
            ConfigKeys.LoginKeys.TitsInTops,
            ConfigKeys.LoginKeys.Newgrounds,
            ConfigKeys.LoginKeys.Nijie
        ];
        foreach (var site in siteLogins)
        {
            config.Logins[site] = new Credentials();
        }

        config.Keys = new Dictionary<string, string>();
        string[] siteKeys = [
            ConfigKeys.KeyKeys.Imgur,
            ConfigKeys.KeyKeys.Google,
            ConfigKeys.KeyKeys.Dropbox,
            ConfigKeys.KeyKeys.Pixeldrain
        ];
        foreach (var site in siteKeys)
        {
            config.Keys[site] = "";
        }

        config.Cookies = new Dictionary<string, string>();
        string[] siteCookies = [
            ConfigKeys.CookieKeys.Twitter,
            ConfigKeys.CookieKeys.Newgrounds,
            ConfigKeys.CookieKeys.Porn3dx,
            ConfigKeys.CookieKeys.Pornhub,
            ConfigKeys.CookieKeys.Thothub,
            ConfigKeys.CookieKeys.Kemono,
            ConfigKeys.CookieKeys.SimpCity
        ];
        foreach (var site in siteCookies)
        {
            config.Cookies[site] = "";
        }

        config.Custom = new Dictionary<string, Dictionary<string, string>>
        {
            [ConfigKeys.CustomKeys.V2PH] = new()
            {
                ["frontend"] = "",
                ["frontend-rmt"] = "",
                ["cf_clearance"] = ""
            },
            [ConfigKeys.CustomKeys.GoFile] = new()
            {
                ["accountToken"] = "",
                ["loginLink"] = ""
            }
        };

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