namespace NicheImageRipper.Core.SiteParsing.LinkRules.Rules;

public sealed class Fcww0LinkRule : PenultimateSegmentLinkRule
{
    public override string RuleName => "fcww0";
    public override bool Matches(string url) => url.Contains("fcww0.com");
}