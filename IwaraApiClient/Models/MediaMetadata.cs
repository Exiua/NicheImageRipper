using System.Text.Json.Serialization;

namespace IwaraApiClient.Models;

public class MediaMetadata
{
    [JsonPropertyName("id")]
    public required Guid Id { get; set; }
    [JsonPropertyName("type")]
    public required string Type { get; set; }
    [JsonPropertyName("path")]
    public required string Path { get; set; }
    [JsonPropertyName("name")]
    public required string Name { get; set; }
    [JsonPropertyName("mime")]
    public required string Mime { get; set; }
    [JsonPropertyName("size")]
    public required int Size { get; set; }
    [JsonPropertyName("width")]
    public required int? Width { get; set; }
    [JsonPropertyName("height")]
    public required int? Height { get; set; }
    [JsonPropertyName("duration")]
    public required int? Duration { get; set; }
    [JsonPropertyName("numThumbnails")]
    public required int? NumThumbnails  { get; set; }
    [JsonPropertyName("animatedPreview")]
    public required bool AnimatedPreview { get; set; }
    [JsonPropertyName("createdAt")]
    public required DateTime CreatedAt { get; set; }
    [JsonPropertyName("updatedAt")]
    public required DateTime UpdatedAt { get; set; }
}