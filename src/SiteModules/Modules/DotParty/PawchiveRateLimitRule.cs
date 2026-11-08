using NicheImageRipper.Sdk.SiteParsing;

namespace NicheImageRipper.SiteModules.Modules.DotParty;

public sealed class PawchiveRateLimitRule : IRateLimitRule
{
    public IReadOnlyList<string> Domains { get; } = ["pawchive.pw", "file.pawchive.pw"];
    public int MaxRequests => 1;
    public TimeSpan Interval { get; } = TimeSpan.FromSeconds(1);
}