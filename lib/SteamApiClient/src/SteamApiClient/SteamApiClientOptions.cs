using SteamKit2.Authentication;

namespace SteamApiClient;

public sealed record SteamApiClientOptions
{
    /// <summary>Handles Steam Guard prompts (email/device codes) during credential logon.</summary>
    public IAuthenticator Authenticator { get; init; } = new UserConsoleAuthenticator();

    /// <summary>Persists the refresh token and guard data between runs.</summary>
    public ISteamTokenStore TokenStore { get; init; } = new JsonFileSteamTokenStore("steamConfig.json");

    public int MaxReconnectAttempts { get; init; } = 5;
    public TimeSpan MaxReconnectDelay { get; init; } = TimeSpan.FromSeconds(30);

    public int MaxConcurrentChunkDownloads { get; init; } = 10;

    /// <summary>Attempts per manifest/chunk before giving up. 403s that are fixed by a CDN auth token don't count.</summary>
    public int MaxDownloadAttempts { get; init; } = 5;

    /// <summary>How many times a single manifest/chunk download may request a fresh CDN auth token after a 403.</summary>
    public int MaxCdnAuthTokenRefreshes { get; init; } = 2;

    /// <summary>Initial back-off after a failed CDN request; doubles on each subsequent failure.</summary>
    public TimeSpan InitialRetryDelay { get; init; } = TimeSpan.FromSeconds(30);

    public TimeSpan ServerRentTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public int MaxCdnServers { get; init; } = 100;
    public TimeSpan CdnServerListLifetime { get; init; } = TimeSpan.FromHours(1);
}
