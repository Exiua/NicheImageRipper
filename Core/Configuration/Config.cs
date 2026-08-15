using NicheImageRipper.Core.Utility;

namespace NicheImageRipper.Core.Configuration;

public static class Config
{
    internal const string ConfigPath = "config.json";

    public static GeneralConfig Instance { get; private set; } = LoadConfig<GeneralConfig>();

    private static T CreateTemplateConfig<T>() where T : GeneralConfig, new()
    {
        return new T();
    }

    private static T LoadConfig<T>() where T : GeneralConfig, new()
    {
        if (!File.Exists(ConfigPath))
        {
            return CreateTemplateConfig<T>();
        }

        if (LegacyConfigMigration.TryMigrate<T>(ConfigPath, out var migrated))
        {
            migrated.SaveConfig(); // rewrite once in the new shape; never needs migrating again
            return migrated;
        }

        return JsonUtility.Deserialize<T>(ConfigPath)!;
    }

    public static void ReloadConfig<T>() where T : GeneralConfig, new()
    {
        Instance = LoadConfig<T>();
    }
}