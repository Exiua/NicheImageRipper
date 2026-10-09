using System.Text.Json.Serialization;

namespace NicheImageRipper.SiteModules.Modules.DotParty.Models;

public class DotPartyAttachmentShort
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("path")]
    public string Path { get; set; } = null!;

    [JsonPropertyName("node")]
    public int? Node { get; set; }
}