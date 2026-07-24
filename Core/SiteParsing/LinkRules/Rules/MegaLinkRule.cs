using NicheImageRipper.Core.Enums;

namespace NicheImageRipper.Core.SiteParsing.LinkRules.Rules;

public sealed class MegaLinkRule : ISiteLinkRule
{
    public string RuleName => "mega";
    public bool Matches(string url) => url.Contains("mega.nz");
    public SiteLinkInfo Resolve(string url) => new(url, LinkInfo.Mega);
}