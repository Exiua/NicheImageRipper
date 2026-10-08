using System.Text.Json.Serialization;

namespace PatreonApiClient.Models;

public sealed class ImageAttributes
{
    [JsonPropertyName("alt_text")]
    public string? AltText { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("image_colors")]
    public ImageColors? ImageColors { get; set; }

    [JsonPropertyName("image_icon")]
    public string? ImageIcon { get; set; }

    [JsonPropertyName("image_large")]
    public string? ImageLarge { get; set; }

    [JsonPropertyName("image_medium")]
    public string? ImageMedium { get; set; }

    [JsonPropertyName("image_small")]
    public string? ImageSmall { get; set; }

    [JsonPropertyName("is_fallback")]
    public bool IsFallback { get; set; }

    [JsonPropertyName("prefer_alternate_display")]
    public bool PreferAlternateDisplay { get; set; }

    [JsonPropertyName("primary_image_type")]
    public string? PrimaryImageType { get; set; }
}