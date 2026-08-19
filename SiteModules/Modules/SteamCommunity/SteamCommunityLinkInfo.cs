using NicheImageRipper.Core.DataStructures;

namespace NicheImageRipper.SiteModules.Modules.SteamCommunity;

public static class SteamCommunityLinkInfo
{
    private const string SteamCommunityValue = "steamcommunity";
    public static readonly LinkInfo SteamCommunity = LinkInfoRegistry.Register(SteamCommunityValue);
}