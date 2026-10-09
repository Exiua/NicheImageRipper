using System.Text.Json.Serialization;

namespace IwaraApiClient.Models;

public class Image
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }
    [JsonPropertyName("status")]
    public required string Status { get; set; }
    [JsonPropertyName("slug")]
    public string? Slug { get; set; }
    [JsonPropertyName("title")]
    public string? Title { get; set; }
    [JsonPropertyName("body")]
    public string? Body { get; set; }
    [JsonPropertyName("thumbnail")]
    public required MediaMetadata Thumbnail { get; set; }
    [JsonPropertyName("rating")]
    public required string Rating { get; set; }
    [JsonPropertyName("liked")]
    public required bool Liked { get; set; }
    [JsonPropertyName("numImages")]
    public required int NumImages { get; set; }
    [JsonPropertyName("numLikes")]
    public required int NumLikes { get; set; }
    [JsonPropertyName("numViews")]
    public required int NumViews { get; set; }
    [JsonPropertyName("numComments")]
    public required int NumComments { get; set; }
    [JsonPropertyName("createdAt")]
    public required DateTime CreatedAt { get; set; }
    [JsonPropertyName("updatedAt")]
    public required DateTime UpdatedAt { get; set; }
    [JsonPropertyName("files")]
    public required List<MediaMetadata> Files { get; set; }
    [JsonPropertyName("tags")]
    public required List<Tag> Tags { get; set; }
    [JsonPropertyName("user")]
    public required User User { get; set; }
    [JsonPropertyName("siteId")]
    public required string SiteId { get; set; } 
}