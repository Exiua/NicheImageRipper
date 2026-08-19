using NicheImageRipper.Core.DataStructures;

namespace NicheImageRipper.SiteModules.Modules.Iwara;

public class IwaraLinkInfo : ILinkInfoProvider
{
    private const string IwaraValue = "iwara";
    public static readonly LinkInfo Iwara = LinkInfoRegistry.Register(IwaraValue);
}