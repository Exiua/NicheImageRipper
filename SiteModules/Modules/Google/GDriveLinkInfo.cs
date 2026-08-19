using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;

namespace NicheImageRipper.SiteModules.Modules.Google;

public static class GDriveLinkInfo
{
    private const string GDriveValue = "gdrive";
    public static readonly LinkInfo GDrive = LinkInfoRegistry.Register(GDriveValue);
}