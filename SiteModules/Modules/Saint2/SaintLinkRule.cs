using NicheImageRipper.Core.SiteParsing.LinkRules;

namespace NicheImageRipper.SiteModules.Modules.Saint2;

public sealed class SaintLinkRule : ISiteLinkRule
{
    public string RuleName => "saint";
    public bool Matches(string url) => url.Contains("saint.to");
    public SiteLinkInfo Resolve(string url) => new(url, Referer: "https://saint.to/");
}