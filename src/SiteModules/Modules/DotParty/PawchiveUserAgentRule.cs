using NicheImageRipper.Sdk.SiteParsing;

namespace NicheImageRipper.SiteModules.Modules.DotParty;

public sealed class PawchiveUserAgentRule :IUserAgentRule
{
    public IReadOnlyList<string> Domains { get; } = ["pawchive.com"];
    public string UserAgent => "NicheImageRipper";
}