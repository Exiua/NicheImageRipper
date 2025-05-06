using Core.Utility;

namespace Core.Configuration;

public class Config
{
    internal const string ConfigPath = "config.json";

    public static GeneralConfig Instance { get; private set; } = LoadConfig<GeneralConfig>();
    
    private static T CreateTemplateConfig<T>() where T : GeneralConfig
    {
        var config = (T)Activator.CreateInstance(typeof(T), [ true ])!;

        string[] siteLogins = [
            ConfigKeys.LoginKeys.SexyEGirls,
            ConfigKeys.LoginKeys.DeviantArt,
            ConfigKeys.LoginKeys.Mega,
            ConfigKeys.LoginKeys.TitsInTops,
            ConfigKeys.LoginKeys.Newgrounds,
            ConfigKeys.LoginKeys.Nijie,
            ConfigKeys.LoginKeys.Danbooru,
            ConfigKeys.LoginKeys.Gelbooru,
            ConfigKeys.LoginKeys.Rule34,
            ConfigKeys.LoginKeys.Yandere,
            ConfigKeys.LoginKeys.E621,
        ];
        foreach (var site in siteLogins)
        {
            config.Logins[site] = new Credentials
            {
                Username = "",
                Password = ""
            };
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