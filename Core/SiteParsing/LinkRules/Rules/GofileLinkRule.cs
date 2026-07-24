namespace NicheImageRipper.Core.SiteParsing.LinkRules.Rules;

public sealed class GofileLinkRule : ISiteLinkRule
{
    public string RuleName => "gofile";
    public bool Matches(string url) => url.Contains("gofile.io");
    public SiteLinkInfo Resolve(string url) => new(url, Referer: "https://gofile.io/");
}