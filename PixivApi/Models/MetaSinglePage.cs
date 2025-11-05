using System.Text.Json.Serialization;

namespace PixivApi.Models;

public class MetaSinglePage : PixivModel
{
    [JsonPropertyName("original_image_url")]
    public string? OriginalImageUrl { get; set; }
}
