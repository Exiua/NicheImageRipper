using System.Text.Json.Serialization;

namespace PatreonApiClient.Models;

public sealed class CornerColors
{
    [JsonPropertyName("bottom_left")]
    public string? BottomLeft { get; set; }

    [JsonPropertyName("bottom_right")]
    public string? BottomRight { get; set; }

    [JsonPropertyName("top_left")]
    public string? TopLeft { get; set; }

    [JsonPropertyName("top_right")]
    public string? TopRight { get; set; }
}