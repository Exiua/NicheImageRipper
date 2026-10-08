using System.Text.Json.Serialization;

namespace PatreonApiClient.Models;

public sealed class PostThumbnail
{
    [JsonPropertyName("default")]
    public string? Default { get; set; }

    [JsonPropertyName("default_blurred")]
    public string? DefaultBlurred { get; set; }

    [JsonPropertyName("default_blurred_small")]
    public string? DefaultBlurredSmall { get; set; }

    [JsonPropertyName("default_large")]
    public string? DefaultLarge { get; set; }

    [JsonPropertyName("default_small")]
    public string? DefaultSmall { get; set; }

    [JsonPropertyName("height")]
    public int? Height { get; set; }

    [JsonPropertyName("original")]
    public string? Original { get; set; }

    [JsonPropertyName("thumbnail")]
    public string? Thumbnail { get; set; }

    [JsonPropertyName("thumbnail_large")]
    public string? ThumbnailLarge { get; set; }

    [JsonPropertyName("thumbnail_small")]
    public string? ThumbnailSmall { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("width")]
    public int? Width { get; set; }
}