using System.Text.Json.Serialization;

namespace MangaDexLibrary.DataStructures;

public class MangaMetadata
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }
    [JsonPropertyName("type")]
    public string Type { get; set; } = null!;
    [JsonPropertyName("attributes")]
    public MangaAttributes Attributes { get; set; } = null!;
    [JsonPropertyName("relationships")]
    public Relationship[] Relationships { get; set; } = null!;
}