using System.Collections.Concurrent;
using Serilog;

namespace SteamApiClient;

internal sealed class CdnServerPool(IReadOnlyList<SteamKit2.CDN.Server> servers)
{
    private readonly ConcurrentBag<SteamKit2.CDN.Server> _available = new(servers);
    private readonly ConcurrentBag<SteamKit2.CDN.Server> _broken = [];
    private readonly ILogger _logger = Log.ForContext<CdnServerPool>();

    public bool TryRent(out SteamKit2.CDN.Server server)
    {
        if (_available.TryTake(out server!))
        {
            return true;
        }

        // All servers exhausted — try recycling broken ones as last resort
        if (_broken.TryTake(out server!))
        {
            _logger.Warning("All healthy servers exhausted, retrying broken server {Host}", server.Host);
            return true;
        }

        return false;
    }

    public void Return(SteamKit2.CDN.Server server)
    {
        _available.Add(server);
    }

    public void MarkBroken(SteamKit2.CDN.Server server)
    {
        _logger.Warning("Marking server {Host} as broken", server.Host);
        _broken.Add(server);
    }

    public int AvailableCount => _available.Count;
    public int BrokenCount => _broken.Count;
}