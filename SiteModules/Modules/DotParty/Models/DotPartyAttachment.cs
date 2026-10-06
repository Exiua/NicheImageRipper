using System.Text.Json.Serialization;

namespace NicheImageRipper.SiteModules.Modules.DotParty.Models;

public class DotPartyAttachment
{
    [JsonPropertyName("extension")]
    public string Extension { get; set; } = null!;
    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;
    [JsonPropertyName("name_extension")]
    public string NameExtension { get; set; } = null!;
    [JsonPropertyName("path")]
    public string Path { get; set; } = null!;
    [JsonPropertyName("server")]
    public string Server { get; set; } = null!;
    [JsonPropertyName("stem")]
    public string Stem { get; set; } = null!;
}