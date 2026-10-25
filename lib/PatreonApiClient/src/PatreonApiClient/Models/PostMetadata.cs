using System.Text.Json;
using System.Text.Json.Serialization;

namespace PatreonApiClient.Models;

public sealed class PostMetadata
{
    // Absent on some posts, empty on others
    [JsonPropertyName("image_order")]
    public List<string> ImageOrder { get; set; } = [];

    // Always empty object in sample — shape unknown, left as raw JSON
    [JsonPropertyName("platform")]
    public JsonElement? Platform { get; set; }
}