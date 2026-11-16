using System.Text.Json.Serialization;

namespace NicheImageRipper.SiteModules.Modules.DotParty.Models;

public class DotPartyProfile
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;
}
