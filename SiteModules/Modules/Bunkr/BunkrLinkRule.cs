using Sdk.SiteParsing;

namespace NicheImageRipper.SiteModules.Modules.Bunkr;

public sealed class BunkrLinkRule : ISiteLinkRule
{
    public string RuleName => "bunkr";
    public bool Matches(string url) => url.Contains("bunkr.") || url.Contains("bunkr-cache.");
    public SiteLinkInfo Resolve(string url) => new(url, Referer: "https://get.bunkrr.su/");
}