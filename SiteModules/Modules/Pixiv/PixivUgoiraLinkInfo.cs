using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;

namespace NicheImageRipper.SiteModules.Modules.Pixiv;

public static class PixivUgoiraLinkInfo
{
    private const string PixivUgoiraValue = "pixiv-ugoira";
    public static readonly LinkInfo PixivUgoira = LinkInfoRegistry.Register(PixivUgoiraValue);
}