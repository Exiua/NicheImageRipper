using NicheImageRipper.Sdk.Utility;

namespace NicheImageRipper.Sdk.Configuration;

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

        // TODO: Remove this migration logic in a future version (note added: v5.0.0)
        if (LegacyConfigMigration.TryMigrate<T>(ConfigPath, out var migrated))
        {
            migrated.SaveConfig(); // rewrite once in the new shape; never needs migrating again
            return migrated;
        }

        var config = JsonUtility.Deserialize<T>(ConfigPath)!;
        // TODO: Remove this migration logic in a future version (added: v5.0.0)
        config.MigrateLegacy();
        return config;
    }

    public static void ReloadConfig<T>() where T : GeneralConfig, new()
    {
        Instance = LoadConfig<T>();
    }
}