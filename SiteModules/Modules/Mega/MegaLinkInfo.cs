using NicheImageRipper.Core.DataStructures;

namespace NicheImageRipper.SiteModules.Modules.Mega;

public class MegaLinkInfo : ILinkInfoProvider
{
    public const string MegaValue = "mega";
    public static readonly LinkInfo Mega = LinkInfoRegistry.Register(MegaValue);
}