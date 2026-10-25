using System.Text.Json.Serialization;

namespace PatreonApiClient.Models;

public sealed class MediaAttributes
{
    [JsonPropertyName("display")]
    public MediaDisplay? Display { get; set; }

    [JsonPropertyName("download_url")]
    public string? DownloadUrl { get; set; }

    [JsonPropertyName("file_name")]
    public string? FileName { get; set; }

    [JsonPropertyName("image_urls")]
    public ImageUrlSet? ImageUrls { get; set; }

    [JsonPropertyName("metadata")]
    public MediaMetadata? Metadata { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }
}