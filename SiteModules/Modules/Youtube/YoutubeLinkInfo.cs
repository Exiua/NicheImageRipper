
using Sdk.DataStructures;

namespace NicheImageRipper.SiteModules.Modules.Youtube;

public class YoutubeLinkInfo : ILinkInfoProvider
{
    private const string YoutubeVideoValue = "youtube-video";
    public static readonly LinkInfo YoutubeVideo = LinkInfoRegistry.Register(YoutubeVideoValue);
}