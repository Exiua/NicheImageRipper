using NicheImageRipper.Core.DataStructures;

namespace NicheImageRipper.SiteModules.Modules.GoFile;

public static class GoFileLinkInfo
{
    private const string GoFileValue = "gofile";
    public static readonly LinkInfo GoFile = LinkInfoRegistry.Register(GoFileValue);
}