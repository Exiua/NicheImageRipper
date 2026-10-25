using System.Text.Json.Serialization;

namespace PatreonApiClient.Models;

public sealed class MediaDisplay
{
    [JsonPropertyName("height")]
    public int? Height { get; set; }

    // Only present on some media entries
    [JsonPropertyName("image_colors")]
    public ImageColors? ImageColors { get; set; }

    [JsonPropertyName("media_id")]
    public long MediaId { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("width")]
    public int? Width { get; set; }
}