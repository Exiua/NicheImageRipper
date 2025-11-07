using Core.Utility;

namespace Core.Configuration;

public class Config
{
    internal const string ConfigPath = "config.json";

    public static GeneralConfig Instance { get; private set; } = LoadConfig<GeneralConfig>();
    
    private static T CreateTemplateConfig<T>() where T : GeneralConfig
    {
        var config = (T)Activator.CreateInstance(typeof(T), [ true ])!;
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