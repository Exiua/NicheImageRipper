using Core.DataStructures;
using JetBrains.Annotations;

namespace Core.SiteParsing;

public class PartialSaveEntry
{
    [UsedImplicitly]
    public string Cookies { get; set; } = null!;
    
    [UsedImplicitly]
    public string Referer { get; set; } = null!;
    
    [UsedImplicitly]
    public RipInfo RipInfo { get; set; } = null!;
}