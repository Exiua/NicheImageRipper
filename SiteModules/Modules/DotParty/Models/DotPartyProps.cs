using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace NicheImageRipper.SiteModules.Modules.DotParty.Models;

public class DotPartyProps
{
    [JsonPropertyName("flagged")]
    public string? Flagged { get; set; }

    [JsonPropertyName("revisions")]
    public List<List<JsonNode>> Revisions { get; set; } = null!; // Each sublist contains [revision number, DotPartyPostFull]
}