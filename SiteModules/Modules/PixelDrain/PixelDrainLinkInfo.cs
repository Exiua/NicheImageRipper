using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;

namespace NicheImageRipper.SiteModules.Modules.PixelDrain;

public static class PixelDrainLinkInfo
{
    private const string PixelDrainValue = "pixeldrain";
    public static readonly LinkInfo PixelDrain = LinkInfoRegistry.Register(PixelDrainValue);
}