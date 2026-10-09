using System.Text.Json.Serialization;

namespace IwaraApiClient.Models;

public class Video
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }
    [JsonPropertyName("slug")]
    public required string Slug { get; set; }
    [JsonPropertyName("title")]
    public required string Title { get; set; }
    [JsonPropertyName("body")]
    public string? Body { get; set; }
    [JsonPropertyName("status")]
    public required string Status { get; set; }
    [JsonPropertyName("rating")]
    public required string Rating { get; set; }
    [JsonPropertyName("private")]
    public required bool Private { get; set; }
    [JsonPropertyName("unlisted")]
    public required bool Unlisted { get; set; }
    [JsonPropertyName("thumbnail")]
    public required int Thumbnail { get; set; }
    [JsonPropertyName("embedUrl")]
    public string? EmbedUrl { get; set; }
    [JsonPropertyName("liked")]
    public required bool Liked { get; set; }
    [JsonPropertyName("numLikes")]
    public required int NumLikes { get; set; }
    [JsonPropertyName("numViews")]
    public required int NumViews { get; set; }
    [JsonPropertyName("numComments")]
    public required int NumComments { get; set; }
    [JsonPropertyName("file")]
    public required MediaMetadata File { get; set; }
    [JsonPropertyName("customThumbnail")]
    public MediaMetadata? CustomThumbnail { get; set; }
    [JsonPropertyName("user")]
    public required User User { get; set; }
    [JsonPropertyName("tags")]
    public required List<Tag> Tags { get; set; }
    [JsonPropertyName("siteId")]
    public required string SiteId { get; set; }
    [JsonPropertyName("createdAt")]
    public required DateTime CreatedAt { get; set; }
    [JsonPropertyName("updatedAt")]
    public required DateTime UpdatedAt { get; set; }
    [JsonPropertyName("fileUrl")]
    public string? FileUrl { get; set; }
}

public class Tag
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }
    [JsonPropertyName("type")]  
    public required string Type { get; set; }
    [JsonPropertyName("sensitive")]
    public required bool Sensitive { get; set; }
}