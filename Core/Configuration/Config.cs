using Core.Utility;

namespace Core.Configuration;

public class Config
{
    internal const string ConfigPath = "config.json";

    public static GeneralConfig Instance { get; private set; } = LoadConfig<GeneralConfig>();
    
    private static T CreateTemplateConfig<T>() where T : GeneralConfig
    {
        var config = (T)Activator.CreateInstance(typeof(T), [ true ])!;

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