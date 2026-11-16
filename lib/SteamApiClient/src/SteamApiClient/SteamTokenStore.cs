using System.Text.Json;
using Serilog;

namespace SteamApiClient;

public sealed record SteamStoredLogin
{
    /// <summary>Account the token belongs to. Empty for files written before this was tracked.</summary>
    public string AccountName { get; init; } = "";

    public string RefreshToken { get; init; } = "";
    public string GuardData { get; init; } = "";
}

public interface ISteamTokenStore
{
    SteamStoredLogin? Load();
    void Save(SteamStoredLogin login);
    void Clear();
}

/// <summary>Stores the login as plain-text JSON. Treat the file as a credential.</summary>
public sealed class JsonFileSteamTokenStore(string path) : ISteamTokenStore
{
    private static readonly ILogger Logger = Log.ForContext<JsonFileSteamTokenStore>();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly Lock _lock = new();

    public SteamStoredLogin? Load()
    {
        lock (_lock)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<SteamStoredLogin>(File.ReadAllText(path), JsonOptions);
            }
            catch (JsonException e)
            {
                Logger.Warning(e, "Ignoring unreadable Steam token file {Path}", path);
                return null;
            }
        }
    }

    public void Save(SteamStoredLogin login)
    {
        lock (_lock)
        {
            File.WriteAllText(path, JsonSerializer.Serialize(login, JsonOptions));
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            File.Delete(path);
        }
    }
}
