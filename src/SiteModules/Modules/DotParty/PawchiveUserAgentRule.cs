using NicheImageRipper.Sdk.SiteParsing;

namespace NicheImageRipper.SiteModules.Modules.DotParty;

public sealed class PawchiveUserAgentRule :IUserAgentRule
{
    public IReadOnlyList<string> Domains { get; } = ["pawchive.pw", "file.pawchive.pw"];
    public string UserAgent => "NicheImageRipper";
}