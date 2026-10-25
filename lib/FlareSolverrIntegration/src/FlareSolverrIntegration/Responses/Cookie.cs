using System.Text.Json.Serialization;

namespace FlareSolverrIntegration.Responses;

public class Cookie
{
    [JsonPropertyName("domain")]
    public string Domain { get; set; } = null!;
    [JsonPropertyName("expiry")]
    public long Expiry { get; set; }
    [JsonPropertyName("httpOnly")]
    public bool HttpOnly { get; set; }
    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;
    [JsonPropertyName("path")]
    public string Path { get; set; } = null!;
    [JsonPropertyName("sameSite")]
    public string SameSite { get; set; } = null!;
    [JsonPropertyName("secure")]
    public bool Secure { get; set; }
    [JsonPropertyName("value")]
    public string Value { get; set; } = null!;
}