using System.Text.Json.Serialization;

namespace NicheImageRipper.SiteModules.Modules.DotParty.Models;

public class DotPartyPreview
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = null!;

    [JsonPropertyName("server")]
    public string Server { get; set; } = null!;

    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;

    [JsonPropertyName("path")]
    public string Path { get; set; } = null!;
}