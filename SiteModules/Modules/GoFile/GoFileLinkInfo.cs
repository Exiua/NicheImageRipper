using NicheImageRipper.Core.DataStructures;

namespace NicheImageRipper.SiteModules.Modules.GoFile;

public class GoFileLinkInfo : ILinkInfoProvider
{
    private const string GoFileValue = "gofile";
    public static readonly LinkInfo GoFile = LinkInfoRegistry.Register(GoFileValue);
}