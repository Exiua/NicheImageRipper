using Serilog;

namespace NicheImageRipper.Sdk.SiteParsing;

/// <summary>
///     Resolves the User-Agent to send to a URL from the loaded <see cref="IUserAgentRule"/>s. Rules live in Core and in
///     site modules, so every loaded assembly is scanned; Core refreshes the list once modules have loaded.
/// </summary>
public static class UserAgentRuleRegistry
{
    private static readonly ILogger Logger = Log.ForContext(typeof(UserAgentRuleRegistry));

    /// <summary>(domain, user agent) pairs, longest domain first so the most specific rule wins.</summary>
    private static IReadOnlyList<(string Domain, string UserAgent)> _rules = DiscoverRules();

    private static string GenericUserAgent => Configuration.Config.Instance.UserAgent;

    /// <summary>Re-scans loaded assemblies; called by Core after loading site modules.</summary>
    internal static void Refresh() => _rules = DiscoverRules();

    private static List<(string, string)> DiscoverRules()
    {
        var rules = DomainRules.Discover<IUserAgentRule>()
                               .SelectMany(rule => rule.Domains.Select(domain => (Domain: domain.ToLowerInvariant(),
                                   rule.UserAgent)))
                               .OrderByDescending(r => r.Domain.Length)
                               .ToList();

        foreach (var duplicate in rules.GroupBy(r => r.Domain).Where(g => g.Count() > 1))
        {
            // Same domain claimed twice is a module bug; the first one found wins
            Logger.Warning("Multiple User-Agent rules claim {Domain}; using {UserAgent}", duplicate.Key,
                duplicate.First().UserAgent);
        }

        return rules;
    }

    /// <summary>
    ///     The User-Agent to send to <paramref name="url"/>: a matching rule's when <paramref name="userAgent"/> is
    ///     the generic configured one, otherwise <paramref name="userAgent"/> unchanged, so one a parser set
    ///     deliberately (e.g. FlareSolverr's) is left alone.
    /// </summary>
    public static string Resolve(string url, string userAgent)
    {
        if (userAgent != GenericUserAgent || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return userAgent;
        }

        var host = uri.Host;
        foreach (var (domain, ruleUserAgent) in _rules)
        {
            if (DomainRules.HostMatches(host, domain))
            {
                return ruleUserAgent;
            }
        }

        return userAgent;
    }
}
