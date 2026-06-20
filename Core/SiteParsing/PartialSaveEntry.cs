using JetBrains.Annotations;
using NicheImageRipper.Core.DataStructures;

namespace NicheImageRipper.Core.SiteParsing;

public class PartialSaveEntry
{
    [UsedImplicitly]
    public string Cookies { get; set; } = null!;
    
    [UsedImplicitly]
    public string Referer { get; set; } = null!;
    
    [UsedImplicitly]
    public RipInfo RipInfo { get; set; } = null!;
}