using System.Diagnostics;

namespace NicheImageRipper.Core.SiteParsing.LinkRules;

public static class SiteLinkRuleRegistry
{
    private static readonly IReadOnlyList<ISiteLinkRule> Rules = DiscoverRules();

    private static List<ISiteLinkRule> DiscoverRules()
    {
        var ruleInterface = typeof(ISiteLinkRule);
        return ruleInterface.Assembly.GetTypes()
                            .Where(t => ruleInterface.IsAssignableFrom(t) && t is { IsInterface: false, IsAbstract: false })
                            .Select(t => (ISiteLinkRule)Activator.CreateInstance(t)!)
                            .ToList();
    }

    public static ISiteLinkRule? FindMatch(string url)
    {
        var matches = Rules.Where(r => r.Matches(url)).ToList();
        Debug.Assert(matches.Count <= 1, $"Multiple site rules matched URL: {url} ({string.Join(", ", matches.Select(r => r.RuleName))})");
        return matches.FirstOrDefault();
    }
}