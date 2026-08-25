using Sdk.SiteParsing;

namespace NicheImageRipper.SiteModules.Modules.Shameless;

public sealed class ShamelessLinkRule : ISiteLinkRule
{
    public string RuleName => "shameless";
    public bool Matches(string url) => url.Contains("shameless.com");
    public SiteLinkInfo Resolve(string url) => new(url, Filename: url.Split("/")[8]);
}