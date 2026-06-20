using NicheImageRipper.Core.Utility;

namespace NicheImageRipper.Core.Configuration;

public static class Config
{
    internal const string ConfigPath = "config.json";

    public static GeneralConfig Instance { get; private set; } = LoadConfig<GeneralConfig>();
    
    private static T CreateTemplateConfig<T>() where T : GeneralConfig, new()
    {
        var config = new T();
        return config;
    }

    private static T LoadConfig<T>() where T : GeneralConfig, new()
    {
        return File.Exists(ConfigPath)
            ? JsonUtility.Deserialize<T>(ConfigPath)!
            : CreateTemplateConfig<T>();
    }
    
    public static void ReloadConfig<T>() where T : GeneralConfig, new()
    {
        Instance = LoadConfig<T>();
    }
}