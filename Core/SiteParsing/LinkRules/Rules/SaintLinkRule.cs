using NicheImageRipper.Core.Enums;

namespace NicheImageRipper.Core.SiteParsing.LinkRules.Rules;

public sealed class SaintLinkRule : ISiteLinkRule
{
    public string RuleName => "saint";
    public bool Matches(string url) => url.Contains("saint.to");
    public SiteLinkInfo Resolve(string url) => new(url, Referer: "https://saint.to/");
}