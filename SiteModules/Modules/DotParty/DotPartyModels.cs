using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace NicheImageRipper.SiteModules.Modules.DotParty;

public class DotPartyCache
{
    public string DirName { get; set; } = null!;
    public List<DotPartyPostResponse> Posts { get; set; } = null!;
}

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

public class DotPartyFile
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("path")]
    public string? Path { get; set; }
}

public class DotPartyAttachment
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("path")]
    public string Path { get; set; } = null!;
}

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

public class DotPartyVideo
{
    [JsonExtensionData]
    private Dictionary<string, JsonElement> Fields { get; set; } = null!;
}

public class DotPartyProps
{
    [JsonPropertyName("flagged")]
    public string? Flagged { get; set; }

    [JsonPropertyName("revisions")]
    public List<List<JsonNode>> Revisions { get; set; } = null!; // Each sublist contains [revision number, DotPartyPostFull]
}