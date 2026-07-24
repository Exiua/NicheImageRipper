namespace NicheImageRipper.Core.SiteParsing.LinkRules;

public abstract class PenultimateSegmentLinkRule : ISiteLinkRule
{
    public abstract string RuleName { get; }
    public abstract bool Matches(string url);
    public virtual SiteLinkInfo Resolve(string url) => new(url, Filename: url.Split("/")[^2]);
}