using System.Text.Json.Serialization;

namespace PixivApi.Models;

public class UserIllustrations : PixivModel
{
    [JsonPropertyName("user")]
    public UserInfo User { get; set; } = null!;
    [JsonPropertyName("illusts")]
    public List<IllustrationInfo> Illusts { get; set; } = null!;
    [JsonPropertyName("next_url")]
    public string? NextUrl { get; set; }
}
