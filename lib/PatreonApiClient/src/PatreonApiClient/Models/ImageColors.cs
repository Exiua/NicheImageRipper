using System.Text.Json.Serialization;

namespace PatreonApiClient.Models;

public sealed class ImageColors
{
    [JsonPropertyName("average_colors_of_corners")]
    public CornerColors? AverageColorsOfCorners { get; set; }

    [JsonPropertyName("dominant_color")]
    public string? DominantColor { get; set; }

    [JsonPropertyName("palette")]
    public List<string> Palette { get; set; } = [];

    [JsonPropertyName("text_color")]
    public string? TextColor { get; set; }
}