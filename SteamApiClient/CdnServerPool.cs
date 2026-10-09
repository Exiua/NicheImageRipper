using System.Collections.Concurrent;
using Serilog;

namespace SteamApiClient;

internal sealed class CdnServerPool
{
    private readonly ILogger _logger = Log.ForContext<CdnServerPool>();
    private readonly SemaphoreSlim _availableSignal;
    private readonly ConcurrentBag<SteamKit2.CDN.Server> _available;
    private readonly ConcurrentBag<SteamKit2.CDN.Server> _broken = [];
    private readonly ConcurrentDictionary<string, byte> _recycledHosts = new();

    public CdnServerPool(IReadOnlyList<SteamKit2.CDN.Server> servers)
    {
        _available = [.. servers];
        _availableSignal = new SemaphoreSlim(servers.Count, servers.Count);
    }

    /// <summary>
    /// Waits for a server to become available, up to <paramref name="timeout"/>. Prefers healthy servers;
    /// falls back to a previously-broken server only once no healthy server has become available in time.
    /// </summary>
    public async Task<SteamKit2.CDN.Server?> RentAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (await _availableSignal.WaitAsync(timeout, cancellationToken))
        {
            if (_available.TryTake(out var server))
            {
                return server;
            }

            // Signal/bag briefly out of sync under contention
            _availableSignal.Release();
        }

        // Nothing freed up in time — recycle a broken server as a last resort. This rental does NOT hold a
        // semaphore permit, so Return() must not release one for it either (see Return below).
        if (_broken.TryTake(out var recycled))
        {
            _logger.Warning("No healthy server became available, retrying broken server {Host}", recycled.Host);
            _recycledHosts.TryAdd(recycled.Host!, 0);
            return recycled;
        }

        return null;
    }

    public void Return(SteamKit2.CDN.Server server)
    {
        if (_recycledHosts.TryRemove(server.Host!, out _))
        {
            _available.Add(server);
            return;
        }

        _available.Add(server);
        _availableSignal.Release();
    }

    /// <summary>Marks a server as temporarily unavailable due to a likely-transient failure (rate limiting,
    /// timeout, 5xx). It becomes eligible again after <paramref name="cooldown"/> via a background re-add,
    /// so it isn't excluded from the pool for the rest of the download.</summary>
    public void MarkTransientFailure(SteamKit2.CDN.Server server, TimeSpan cooldown)
    {
        _logger.Warning("Server {Host} hit a transient failure, cooling down for {Cooldown}s", server.Host, cooldown.TotalSeconds);
        _recycledHosts.TryRemove(server.Host!, out _);
        _ = Task.Delay(cooldown).ContinueWith(_ => Return(server));
    }


    /// <summary>Marks a server as broken. Only used as a last-resort fallback if the pool otherwise runs dry.</summary>
    public void MarkBroken(SteamKit2.CDN.Server server)
    {
        _recycledHosts.TryRemove(server.Host!, out _);
        _logger.Warning("Marking server {Host} as broken", server.Host);
        _broken.Add(server);
    }

    public int AvailableCount => _available.Count;
    public int BrokenCount => _broken.Count;
}