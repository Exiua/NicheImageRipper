namespace NicheImageRipper.SiteModules.Modules.Six9Tang;

public sealed class Tang69LinkRule : PenultimateSegmentLinkRule
{
    public override string RuleName => "69tang";
    public override bool Matches(string url) => url.Contains("69tang.org");
}