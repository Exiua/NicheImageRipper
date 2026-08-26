using JetBrains.Annotations;
using NicheImageRipper.Sdk.DataStructures;

namespace NicheImageRipper.Sdk.SiteParsing;

public class PartialSaveEntry
{
    [UsedImplicitly]
    public string Cookies { get; set; } = null!;
    
    [UsedImplicitly]
    public string Referer { get; set; } = null!;
    
    [UsedImplicitly]
    public RipInfo RipInfo { get; set; } = null!;
}