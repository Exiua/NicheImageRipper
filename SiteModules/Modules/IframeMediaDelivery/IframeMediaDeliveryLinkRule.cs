
using Sdk.DataStructures;
using Sdk.SiteParsing;

namespace NicheImageRipper.SiteModules.Modules.IframeMediaDelivery;

public sealed class IframeMediaDeliveryLinkRule : ISiteLinkRule
{
    public string RuleName => "iframe-mediadelivery";

    public bool Matches(string url) => url.Contains("iframe.mediadelivery.net");

    public SiteLinkInfo Resolve(string url)
    {
        var split = url.Split("}");
        var playlistUrl = split[0].Split("{")[0];
        var referer = split[1];
        var filename = url.Split("/")[^1].Split("?")[0] + ".mp4"; // assume mp4

        return new SiteLinkInfo(playlistUrl, LinkInfo.IframeMedia, referer, filename);
    }
}