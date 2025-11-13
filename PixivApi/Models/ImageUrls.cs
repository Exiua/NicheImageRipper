using System.Text.Json.Serialization;

namespace PixivApi.Models;

public class ImageUrls : PixivModel
{
    [JsonPropertyName("square_medium")]
    public string SquareMedium { get; set; } = null!;
    [JsonPropertyName("medium")]
    public string Medium { get; set; } = null!;
    [JsonPropertyName("large")]
    public string Large { get; set; } = null!;
    // [JsonPropertyName("original")]
    // public string Original { get; set; }
}