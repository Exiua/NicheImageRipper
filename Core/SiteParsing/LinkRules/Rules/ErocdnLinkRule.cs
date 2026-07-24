namespace NicheImageRipper.Core.SiteParsing.LinkRules.Rules;

public sealed class ErocdnLinkRule : ISiteLinkRule
{
    public string RuleName => "erocdn";
    public bool Matches(string url) => url.Contains("erocdn.co");

    public SiteLinkInfo Resolve(string url)
    {
        var parts = url.Split("/");
        var ext = parts[^1].Split(".")[^1];
        return new SiteLinkInfo(url, Filename: $"{parts[^2]}.{ext}");
    }
}