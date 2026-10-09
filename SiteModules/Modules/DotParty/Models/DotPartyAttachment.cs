using System.Text.Json.Serialization;

namespace NicheImageRipper.SiteModules.Modules.DotParty.Models;

public class DotPartyAttachment : DotPartyAttachmentShort
{
    [JsonPropertyName("extension")]
    public string Extension { get; set; } = null!;
    [JsonPropertyName("name_extension")]
    public string NameExtension { get; set; } = null!;
    [JsonPropertyName("server")]
    public string Server { get; set; } = null!;
    [JsonPropertyName("stem")]
    public string Stem { get; set; } = null!;
}