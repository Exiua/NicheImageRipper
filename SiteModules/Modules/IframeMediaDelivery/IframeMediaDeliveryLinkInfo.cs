using NicheImageRipper.Sdk.DataStructures;

namespace NicheImageRipper.SiteModules.Modules.IframeMediaDelivery;

public class IframeMediaDeliveryLinkInfo : ILinkInfoProvider
{
    // Same value it had as a built-in, so saved partial rips still resolve to this strategy
    private const string IframeMediaValue = "iframe-media";

    /// <summary>A Bunny Stream (iframe.mediadelivery.net) embed, downloaded through its DRM handshake.</summary>
    public static readonly LinkInfo IframeMedia = LinkInfoRegistry.Register(IframeMediaValue);
}
