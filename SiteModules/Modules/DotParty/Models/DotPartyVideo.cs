using System.Text.Json;
using System.Text.Json.Serialization;

namespace NicheImageRipper.SiteModules.Modules.DotParty.Models;

public class DotPartyVideo
{
    [JsonExtensionData]
    private Dictionary<string, JsonElement> Fields { get; set; } = null!;
}