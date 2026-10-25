using System.Text.Json.Serialization;

namespace PatreonApiClient.Models;

public sealed class Dimensions
{
    [JsonPropertyName("h")]
    public int H { get; set; }

    [JsonPropertyName("w")]
    public int W { get; set; }
}