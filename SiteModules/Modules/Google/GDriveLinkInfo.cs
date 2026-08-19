using NicheImageRipper.Core.DataStructures;

namespace NicheImageRipper.SiteModules.Modules.Google;

public class GDriveLinkInfo  : ILinkInfoProvider
{
    private const string GDriveValue = "gdrive";
    public static readonly LinkInfo GDrive = LinkInfoRegistry.Register(GDriveValue);
}