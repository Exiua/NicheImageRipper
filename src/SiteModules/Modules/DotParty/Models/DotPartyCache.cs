namespace NicheImageRipper.SiteModules.Modules.DotParty.Models;

public class DotPartyCache
{
    public string DirName { get; set; } = null!;
    public List<DotPartyPostResponse> Posts { get; set; } = null!;

    /// <summary>The update cursor the posts were fetched after; null when every post was fetched.</summary>
    public string? UpdateCursor { get; set; }
}