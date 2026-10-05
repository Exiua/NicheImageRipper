namespace NicheImageRipper.SiteModules.Modules.DotParty.Models;

public class DotPartyCache
{
    public string DirName { get; set; } = null!;
    public List<DotPartyPostResponse> Posts { get; set; } = null!;
}