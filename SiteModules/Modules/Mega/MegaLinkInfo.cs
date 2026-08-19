using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;

namespace NicheImageRipper.SiteModules.Modules.Mega;

public static class MegaLinkInfo
{
    public const string MegaValue = "mega";
    public static readonly LinkInfo Mega = LinkInfoRegistry.Register(MegaValue);
}