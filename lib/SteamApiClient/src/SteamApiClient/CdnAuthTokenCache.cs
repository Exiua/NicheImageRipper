using System.Collections.Concurrent;
using Serilog;
using SteamKit2;
using SteamKit2.Internal;

namespace SteamApiClient;

/// <summary>
/// Caches CDN auth tokens per (depot, host). Concurrent requests for the same key share a single Steam request, and a
/// token that a CDN rejected is replaced at most once no matter how many downloads saw the rejection.
/// </summary>
internal sealed class CdnAuthTokenCache(SteamUnifiedMessages unifiedMessages)
{
    private static readonly ILogger Logger = Log.ForContext<CdnAuthTokenCache>();
    private static readonly TimeSpan ExpiryMargin = TimeSpan.FromSeconds(60);

    private sealed record CdnAuthToken(string Token, DateTimeOffset Expiry)
    {
        public bool IsExpired => DateTimeOffset.UtcNow >= Expiry - ExpiryMargin;
    }

    private readonly ConcurrentDictionary<(uint DepotId, string Host), Lazy<Task<CdnAuthToken?>>> _tokens = new();

    /// <summary>Returns a cached, unexpired token without contacting Steam, or null if there isn't one.</summary>
    public async Task<string?> GetCachedAsync(uint depotId, string host, CancellationToken cancellationToken)
    {
        if (!_tokens.TryGetValue((depotId, host), out var entry))
        {
            return null;
        }

        var token = await AwaitEntryAsync(entry, cancellationToken);
        return token is { IsExpired: false } ? token.Token : null;
    }

    /// <summary>
    /// Returns a token other than <paramref name="rejectedToken"/>, requesting a new one from Steam if needed.
    /// Returns null if Steam refuses to issue one.
    /// </summary>
    public async Task<string?> RefreshAsync(uint appId, uint depotId, string host, string? rejectedToken,
                                            CancellationToken cancellationToken)
    {
        var key = (depotId, host);
        while (true)
        {
            if (!_tokens.TryGetValue(key, out var existing))
            {
                var created = CreateEntry(appId, depotId, host);
                if (_tokens.TryAdd(key, created))
                {
                    return (await AwaitEntryAsync(created, cancellationToken))?.Token;
                }

                continue;
            }

            var current = await AwaitEntryAsync(existing, cancellationToken);
            // Someone else already refreshed it
            if (current is { IsExpired: false } && current.Token != rejectedToken)
            {
                return current.Token;
            }

            var replacement = CreateEntry(appId, depotId, host);
            if (_tokens.TryUpdate(key, replacement, existing))
            {
                return (await AwaitEntryAsync(replacement, cancellationToken))?.Token;
            }
        }
    }

    private Lazy<Task<CdnAuthToken?>> CreateEntry(uint appId, uint depotId, string host) =>
        new(() => RequestTokenAsync(appId, depotId, host));

    private static async Task<CdnAuthToken?> AwaitEntryAsync(Lazy<Task<CdnAuthToken?>> entry,
                                                            CancellationToken cancellationToken)
    {
        try
        {
            return await entry.Value.WaitAsync(cancellationToken);
        }
        catch (Exception e) when (e is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // Failure was already logged by the request; the caller treats a missing token as "no token"
            return null;
        }
    }

    // Shared between callers, so it deliberately isn't tied to any single caller's cancellation token
    private async Task<CdnAuthToken?> RequestTokenAsync(uint appId, uint depotId, string host)
    {
        Logger.Debug("Requesting CDN auth token for depot {DepotId} host {Host}", depotId, host);
        try
        {
            var contentService = unifiedMessages.CreateService<ContentServerDirectory>();
            var response = await contentService.GetCDNAuthToken(new CContentServerDirectory_GetCDNAuthToken_Request
                { app_id = appId, depot_id = depotId, host_name = host });
            if (response.Result != EResult.OK)
            {
                Logger.Warning("CDN auth token request failed for {Host}: {Result}", host, response.Result);
                return null;
            }

            var expiry = DateTimeOffset.FromUnixTimeSeconds(response.Body.expiration_time);
            Logger.Debug("Got CDN auth token for {Host}, expires {Expiry}", host, expiry);
            return new CdnAuthToken(response.Body.token, expiry);
        }
        catch (Exception e)
        {
            Logger.Warning(e, "CDN auth token request failed for {Host}", host);
            return null;
        }
    }
}
