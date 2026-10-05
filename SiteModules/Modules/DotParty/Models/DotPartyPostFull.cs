using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace NicheImageRipper.SiteModules.Modules.DotParty.Models;

public class DotPartyPostFull
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;

    [JsonPropertyName("user")]
    public string User { get; set; } = null!;

    [JsonPropertyName("service")]
    public string Service { get; set; } = null!;

    [JsonPropertyName("title")]
    public string Title { get; set; } = null!;

    [JsonPropertyName("content")]
    public string Content { get; set; } = null!;

    [JsonPropertyName("embed")]
    public JsonElement Embed { get; set; }

    [JsonPropertyName("shared_file")]
    public bool SharedFile { get; set; }

    [JsonPropertyName("added")]
    public string? Added { get; set; }

    [JsonPropertyName("published")]
    public string Published { get; set; } = null!;

    [JsonPropertyName("edited")]
    public string Edited { get; set; } = null!;

    [JsonPropertyName("file")]
    public DotPartyFile File { get; set; } = null!;

    [JsonPropertyName("attachments")]
    public List<DotPartyAttachment> Attachments { get; set; } = null!;

    [JsonPropertyName("poll")]
    public JsonElement? Poll { get; set; }

    [JsonPropertyName("captions")]
    public JsonElement? Captions { get; set; }

    [JsonPropertyName("tags")]
    public JsonArray Tags { get; set; } = null!;

    [JsonPropertyName("incomplete_rewards")]
    public JsonObject? IncompleteRewards { get; set; }

    [JsonPropertyName("next")]
    public string? Next { get; set; }

    [JsonPropertyName("prev")]
    public string? Prev { get; set; }
}