using System.Text.Json.Serialization;

namespace PatreonApiClient.Models;

public sealed class MediaMetadata
{
    [JsonPropertyName("dimensions")]
    public Dimensions? Dimensions { get; set; }

    // Only present on video-type media
    [JsonPropertyName("video_preview_start_ms")]
    public long? VideoPreviewStartMs { get; set; }

    [JsonPropertyName("video_preview_end_ms")]
    public long? VideoPreviewEndMs { get; set; }
}