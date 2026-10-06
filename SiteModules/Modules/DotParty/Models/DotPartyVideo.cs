using System.Text.Json;
using System.Text.Json.Serialization;

namespace NicheImageRipper.SiteModules.Modules.DotParty.Models;

public class DotPartyVideo
{
    [JsonPropertyName("server")]
    public string? Server { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Fields { get; set; }
}