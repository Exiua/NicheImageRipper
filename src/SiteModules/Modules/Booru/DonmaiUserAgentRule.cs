using NicheImageRipper.Sdk.SiteParsing;

namespace NicheImageRipper.SiteModules.Modules.Booru;

/// <summary>Identifies as the app, not a browser, to Danbooru (site, API and cdn.donmai.us).</summary>
public sealed class DonmaiUserAgentRule : IUserAgentRule
{
    public IReadOnlyList<string> Domains => ["donmai.us"];
    public string UserAgent => "NicheImageRipper";
}
