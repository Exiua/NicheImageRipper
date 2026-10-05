using System.Text.Json.Serialization;

namespace NicheImageRipper.SiteModules.Modules.DotParty.Models;

public class DotPartyPostResponse
{
    [JsonPropertyName("post")]
    public DotPartyPostFull Post { get; set; } = null!;

    [JsonPropertyName("attachments")]
    public List<DotPartyAttachment> Attachments { get; set; } = null!;

    [JsonPropertyName("previews")]
    public List<DotPartyPreview> Previews { get; set; } = null!;

    [JsonPropertyName("videos")]
    public List<DotPartyVideo> Videos { get; set; } = null!;

    [JsonPropertyName("props")]
    public DotPartyProps Props { get; set; } = null!;
}