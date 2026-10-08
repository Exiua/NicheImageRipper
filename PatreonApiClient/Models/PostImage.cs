using System.Text.Json.Serialization;

namespace PatreonApiClient.Models;

public sealed class PostImage
{
    [JsonPropertyName("height")]
    public int? Height { get; set; }

    [JsonPropertyName("large_url")]
    public string? LargeUrl { get; set; }

    [JsonPropertyName("thumb_square_large_url")]
    public string? ThumbSquareLargeUrl { get; set; }

    [JsonPropertyName("thumb_square_url")]
    public string? ThumbSquareUrl { get; set; }

    [JsonPropertyName("thumb_url")]
    public string? ThumbUrl { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("width")]
    public int? Width { get; set; }
}