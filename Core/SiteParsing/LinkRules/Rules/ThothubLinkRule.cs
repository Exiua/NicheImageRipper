namespace NicheImageRipper.Core.SiteParsing.LinkRules.Rules;

public sealed class ThothubLinkRule : PenultimateSegmentLinkRule
{
    public override string RuleName => "thothub";
    public override bool Matches(string url) =>
        url.Contains("thothub.lol/") &&
        (url.Contains("/?rnd=") || url.Contains("get_image") || url.Contains("get_file"));
}