using Sdk.SiteParsing;

namespace NicheImageRipper.SiteModules.Modules.Mega;

public sealed class MegaLinkRule : ISiteLinkRule
{
    public string RuleName => "mega";
    public bool Matches(string url) => url.Contains("mega.nz");
    public SiteLinkInfo Resolve(string url) => new(url, MegaLinkInfo.Mega);
}