using NicheImageRipper.Core.SiteParsing.LinkRules;

namespace NicheImageRipper.SiteModules.Modules.Xasiat;

public sealed class XasiatLinkRule : PenultimateSegmentLinkRule
{
    public override string RuleName => "xasiat";
    public override bool Matches(string url) => url.Contains("xasiat.com");
}