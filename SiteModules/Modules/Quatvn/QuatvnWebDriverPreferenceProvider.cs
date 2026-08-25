

namespace NicheImageRipper.SiteModules.Modules.Quatvn;

public sealed class QuatvnWebDriverPreferenceProvider : IWebDriverPreferenceProvider
{
    public bool AppliesTo(string siteName) => siteName == "quatvn";
    public bool RequiresNonHeadless => true;
}