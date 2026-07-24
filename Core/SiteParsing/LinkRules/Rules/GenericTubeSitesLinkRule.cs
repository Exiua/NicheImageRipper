namespace NicheImageRipper.Core.SiteParsing.LinkRules.Rules;

public sealed class GenericTubeSitesLinkRule : PenultimateSegmentLinkRule
{
    private static readonly string[] Domains =
    [
        "porndr.com", "abxxx.com", "love4porn.com", "asianviralhub.com",
        "hdzog.com", "privatehomeclips.com", "x-x-x.tube",
    ];

    public override string RuleName => "generic-tube-sites";
    public override bool Matches(string url) => Domains.Any(url.Contains);
}