using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;

namespace NicheImageRipper.SiteModules.Modules.Iwara;

public static class IwaraLinkInfo
{
    private const string IwaraValue = "iwara";
    public static readonly LinkInfo Iwara = LinkInfoRegistry.Register(IwaraValue);
}