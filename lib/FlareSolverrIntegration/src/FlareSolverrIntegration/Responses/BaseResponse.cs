using System.Text.Json.Serialization;

namespace FlareSolverrIntegration.Responses;

public class BaseResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = null!;
    [JsonPropertyName("message")]
    public string Message { get; set; } = null!;
    [JsonPropertyName("startTimestamp")]
    public long StartTimestamp { get; set; }
    [JsonPropertyName("endTimestamp")]
    public long EndTimestamp { get; set; }
    [JsonPropertyName("version")]
    public string Version { get; set; } = null!;
}
