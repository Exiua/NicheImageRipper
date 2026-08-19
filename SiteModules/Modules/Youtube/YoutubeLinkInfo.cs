using NicheImageRipper.Core.DataStructures;

namespace NicheImageRipper.SiteModules.Modules.Youtube;

public static class YoutubeLinkInfo
{
    private const string YoutubeVideoValue = "youtube-video";
    public static readonly LinkInfo YoutubeVideo = LinkInfoRegistry.Register(YoutubeVideoValue);
}