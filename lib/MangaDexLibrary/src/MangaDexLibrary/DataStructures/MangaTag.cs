using System.Text.Json.Serialization;

namespace MangaDexLibrary.DataStructures;

public class MangaTag
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }
    [JsonPropertyName("type")]
    public string Type { get; set; } = null!;
    [JsonPropertyName("attributes")]
    public TagAttributes Attributes { get; set; } = null!;
    [JsonPropertyName("relationships")]
    public Relationship[] Relationships { get; set; } = null!;
}