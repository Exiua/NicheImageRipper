using System.Text.Json.Serialization;

namespace NHentaiApi.Models;

public class GalleryPage
{
    [JsonPropertyName("number")]
    public int Number { get; set; }

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("thumbnail")]
    public string? Thumbnail { get; set; }

    [JsonPropertyName("thumbnail_width")]
    public int ThumbnailWidth { get; set; }

    [JsonPropertyName("thumbnail_height")]
    public int ThumbnailHeight { get; set; }
}