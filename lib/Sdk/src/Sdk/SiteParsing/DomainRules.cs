namespace NicheImageRipper.Sdk.SiteParsing;

/// <summary>Shared plumbing for the per-domain rule registries (<see cref="UserAgentRuleRegistry"/>, <see cref="DomainRateLimiter"/>).</summary>
internal static class DomainRules
{
    /// <summary>Instantiates every concrete <typeparamref name="T"/> in the loaded assemblies (Core and site modules).</summary>
    public static List<T> Discover<T>()
    {
        var ruleInterface = typeof(T);
        return AppDomain.CurrentDomain
                        .GetAssemblies()
                        .SelectMany(assembly =>
                         {
                             try
                             {
                                 return assembly.GetTypes();
                             }
                             catch
                             {
                                 return [];
                             }
                         })
                        .Where(t => ruleInterface.IsAssignableFrom(t) && t is { IsInterface: false, IsAbstract: false })
                        .Select(t => (T)Activator.CreateInstance(t)!)
                        .ToList();
    }

    /// <summary>Whether <paramref name="host"/> is <paramref name="domain"/> or one of its subdomains.</summary>
    public static bool HostMatches(string host, string domain) =>
        host.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase);
}
