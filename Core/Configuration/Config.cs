using Core.Utility;

namespace Core.Configuration;

public class Config
{
    internal const string ConfigPath = "config.json";

    public static GeneralConfig Instance { get; private set; } = LoadConfig<GeneralConfig>();
    
    private static T CreateTemplateConfig<T>() where T : GeneralConfig
    {
        var config = (T)Activator.CreateInstance(typeof(T), [ true ])!;

        var siteLogins = ConfigKeys.LoginKeys.All;
        foreach (var site in siteLogins)
        {
            config.Logins[site] = new Credentials
            {
                Username = "",
                Password = ""
            };
        }

        config.Keys = new Dictionary<string, string>();
        var siteKeys = ConfigKeys.KeyKeys.All;
        foreach (var site in siteKeys)
        {
            config.Keys[site] = "";
        }

        config.Cookies = new Dictionary<string, string>();
        var siteCookies = ConfigKeys.CookieKeys.All;
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

    private static T LoadConfig<T>() where T : GeneralConfig
    {
        return File.Exists(ConfigPath)
            ? JsonUtility.Deserialize<T>(ConfigPath)!
            : CreateTemplateConfig<T>();
    }
    
    public static void ReloadConfig<T>() where T : GeneralConfig
    {
        Instance = LoadConfig<T>();
    }
}