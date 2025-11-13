using System.Text.Json.Serialization;

namespace PixivApi.Models;

public class MetaPage : PixivModel
{
    [JsonPropertyName("image_urls")]
    public ImageUrls ImageUrls { get; set; } = null!;
}