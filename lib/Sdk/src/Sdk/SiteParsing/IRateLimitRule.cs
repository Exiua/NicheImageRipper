namespace NicheImageRipper.Sdk.SiteParsing;

/// <summary>
///     Caps how many requests start against some domains in any <see cref="Interval"/>. Implementations are discovered
///     by <see cref="DomainRateLimiter"/>; the budget is shared by all of a rule's <see cref="Domains"/> (and their
///     subdomains) and by every rip running in the process.
/// </summary>
public interface IRateLimitRule
{
    /// <summary>Hosts this rule covers; each also matches its subdomains (<c>donmai.us</c> covers <c>cdn.donmai.us</c>).</summary>
    IReadOnlyList<string> Domains { get; }

    /// <summary>Most requests allowed to start within any <see cref="Interval"/>-long window.</summary>
    int MaxRequests { get; }

    TimeSpan Interval { get; }
}
