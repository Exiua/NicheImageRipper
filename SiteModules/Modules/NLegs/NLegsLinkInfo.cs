using NicheImageRipper.Sdk.DataStructures;

namespace NicheImageRipper.SiteModules.Modules.NLegs;

public class NLegsLinkInfo : ILinkInfoProvider
{
    // Same value it had as a built-in, so saved partial rips still resolve to this strategy
    private const string ResolveImageValue = "resolve-image";

    /// <summary>An NLegs/LadyLap image page whose real image URL has to be scraped (solving a captcha if needed).</summary>
    public static readonly LinkInfo ResolveImage = LinkInfoRegistry.Register(ResolveImageValue);
}
