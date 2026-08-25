
using Sdk.DataStructures;

namespace NicheImageRipper.SiteModules.Modules.PixelDrain;

public class PixelDrainLinkInfo : ILinkInfoProvider
{
    private const string PixelDrainValue = "pixeldrain";
    public static readonly LinkInfo PixelDrain = LinkInfoRegistry.Register(PixelDrainValue);
}