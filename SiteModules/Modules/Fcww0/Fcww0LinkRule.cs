using NicheImageRipper.Core.SiteParsing.LinkRules;

namespace NicheImageRipper.SiteModules.Modules.Fcww0;

public sealed class Fcww0LinkRule : PenultimateSegmentLinkRule
{
    public override string RuleName => "fcww0";
    public override bool Matches(string url) => url.Contains("fcww0.com");
}