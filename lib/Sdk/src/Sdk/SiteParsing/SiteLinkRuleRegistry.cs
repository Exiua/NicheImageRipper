using Serilog;

namespace NicheImageRipper.Sdk.SiteParsing;

/// <summary>
///     Finds the <see cref="ISiteLinkRule"/> for a URL. Rules live in Core and in site modules, so every loaded
///     assembly is scanned; Core refreshes the list once modules have loaded.
/// </summary>
public static class SiteLinkRuleRegistry
{
    private static readonly ILogger Logger = Log.ForContext(typeof(SiteLinkRuleRegistry));
    private static readonly HashSet<string> ReportedOverlaps = [];
    private static IReadOnlyList<ISiteLinkRule> _rules = DiscoverRules();

    /// <summary>Re-scans loaded assemblies; called by Core after loading site modules.</summary>
    internal static void Refresh() => _rules = DiscoverRules();

    private static List<ISiteLinkRule> DiscoverRules()
    {
        var ruleInterface = typeof(ISiteLinkRule);
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
                        .Select(t => (ISiteLinkRule)Activator.CreateInstance(t)!)
                        .ToList();
    }

    public static ISiteLinkRule? FindMatch(string url)
    {
        var matches = _rules.Where(r => r.Matches(url)).ToList();
        if (matches.Count > 1)
        {
            // Overlapping rules are a module bug; use the first rather than fail, and say so once per combination
            var names = string.Join(", ", matches.Select(r => r.RuleName));
            lock (ReportedOverlaps)
            {
                if (ReportedOverlaps.Add(names))
                {
                    Logger.Warning("Multiple link rules match {Url}: {Rules}; using {Rule}", url, names,
                        matches[0].RuleName);
                }
            }
        }

        return matches.FirstOrDefault();
    }
}
