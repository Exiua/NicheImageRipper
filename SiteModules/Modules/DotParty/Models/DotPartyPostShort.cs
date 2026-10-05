using System.Text.Json.Serialization;

namespace NicheImageRipper.SiteModules.Modules.DotParty.Models;

public class DotPartyPostShort
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;

    [JsonPropertyName("user")]
    public string User { get; set; } = null!;

    [JsonPropertyName("service")]
    public string Service { get; set; } = null!;

    [JsonPropertyName("title")]
    public string Title { get; set; } = null!;

    [JsonPropertyName("substring")]
    public string Substring { get; set; } = null!;

    [JsonPropertyName("published")]
    public string Published { get; set; } = null!;

    [JsonPropertyName("file")]
    public DotPartyFile File { get; set; } = null!;

    [JsonPropertyName("attachments")]
    public List<DotPartyAttachment> Attachments { get; set; } = null!;
}