using NicheImageRipper.Core.Enums;

namespace NicheImageRipper.SiteModules.Modules.Youtube;

public static class YoutubeLinkInfo
{
    private const string YoutubeVideoValue = "youtube-video";
    public static readonly LinkInfo YoutubeVideo = new(YoutubeVideoValue);
}