using System.Text.Json.Serialization;

namespace MangaDexLibrary.DataStructures;

public class Relationship
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }
    [JsonPropertyName("type")]
    public string Type { get; set; } = null!;
}