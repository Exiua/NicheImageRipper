using System.Diagnostics;
using Serilog;

namespace NicheImageRipper.Sdk.SiteParsing;

/// <summary>
///     Process-wide per-domain request throttling from the loaded <see cref="IRateLimitRule"/>s. Awaited before HTTP
///     requests (via <see cref="RateLimitingHandler"/>) and browser navigation, so concurrent rips share one budget per
///     site. Rules live in Core and in site modules; Core refreshes the list once modules have loaded.
/// </summary>
public static class DomainRateLimiter
{
    private static readonly ILogger Logger = Log.ForContext(typeof(DomainRateLimiter));

    /// <summary>(domain, limiter) pairs, longest domain first so the most specific rule wins.</summary>
    private static IReadOnlyList<(string Domain, SlidingWindowLimiter Limiter)> _limiters = BuildLimiters();

    /// <summary>Re-scans loaded assemblies; called by Core after loading site modules. Resets all budgets.</summary>
    internal static void Refresh() => _limiters = BuildLimiters();

    private static List<(string, SlidingWindowLimiter)> BuildLimiters()
    {
        var limiters = new List<(string Domain, SlidingWindowLimiter Limiter)>();
        foreach (var rule in DomainRules.Discover<IRateLimitRule>())
        {
            if (rule.MaxRequests < 1 || rule.Interval <= TimeSpan.Zero)
            {
                Logger.Warning("Ignoring rate limit rule {Rule}: needs at least 1 request per positive interval",
                    rule.GetType().Name);
                continue;
            }

            // One limiter per rule, so all of its domains draw from the same budget
            var limiter = new SlidingWindowLimiter(rule.MaxRequests, rule.Interval);
            limiters.AddRange(rule.Domains.Select(domain => (domain.ToLowerInvariant(), limiter)));
        }

        foreach (var duplicate in limiters.GroupBy(l => l.Domain).Where(g => g.Count() > 1))
        {
            // Same domain claimed twice is a module bug; the first one found wins
            Logger.Warning("Multiple rate limit rules claim {Domain}; using the first", duplicate.Key);
        }

        return limiters.OrderByDescending(l => l.Domain.Length).ToList();
    }

    /// <summary>Waits until a request to <paramref name="url"/> fits its domain's budget; returns at once if no rule matches.</summary>
    public static Task WaitAsync(string url, CancellationToken cancellationToken = default) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? WaitAsync(uri, cancellationToken)
            : Task.CompletedTask;

    /// <inheritdoc cref="WaitAsync(string, CancellationToken)"/>
    public static Task WaitAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        var host = uri.Host;
        foreach (var (domain, limiter) in _limiters)
        {
            if (DomainRules.HostMatches(host, domain))
            {
                return limiter.WaitAsync(domain, cancellationToken);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>Allows at most <c>maxRequests</c> starts in any <c>interval</c>-long window; waiters go first-come, first-served.</summary>
    private sealed class SlidingWindowLimiter(int maxRequests, TimeSpan interval)
    {
        private readonly Queue<long> _starts = new();
        private readonly SemaphoreSlim _gate = new(1, 1);

        public async Task WaitAsync(string domain, CancellationToken cancellationToken)
        {
            // Holding the gate while delaying keeps waiters in arrival order
            await _gate.WaitAsync(cancellationToken);
            try
            {
                while (true)
                {
                    var now = Stopwatch.GetTimestamp();
                    while (_starts.Count > 0 && Stopwatch.GetElapsedTime(_starts.Peek(), now) >= interval)
                    {
                        _starts.Dequeue();
                    }

                    if (_starts.Count < maxRequests)
                    {
                        _starts.Enqueue(now);
                        return;
                    }

                    var wait = interval - Stopwatch.GetElapsedTime(_starts.Peek(), now);
                    Logger.Debug("Rate limit reached for {Domain}; waiting {Wait}", domain, wait);
                    await Task.Delay(wait, cancellationToken);
                }
            }
            finally
            {
                _gate.Release();
            }
        }
    }
}
