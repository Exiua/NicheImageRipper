using System.Text.Json;

namespace SteamApiClient;

public class SteamConfig
{
    private const string ConfigPath = "steamConfig.json";
    
    public static SteamConfig Instance { get; } = LoadOrCreateConfig();
    
    private static JsonSerializerOptions JsonOptions => new() { WriteIndented = true };
    
    public string RefreshToken { get; set; } = string.Empty;
    public string GuardData { get; set; }  = string.Empty;

    public static SteamConfig LoadOrCreateConfig()
    {
        if (File.Exists(ConfigPath))
        {
            var configRaw = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<SteamConfig>(configRaw) ?? new SteamConfig();
        }
        else
        {
            var config = new SteamConfig();
            Save(config);
            return config;
        }
    }

    public static void Save(SteamConfig? config = null)
    {
        config ??= Instance;
        var configJson = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(ConfigPath, configJson);
    }
}