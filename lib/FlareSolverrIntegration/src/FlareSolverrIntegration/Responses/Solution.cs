using System.Text.Json.Serialization;

namespace FlareSolverrIntegration.Responses;

public class Solution
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = null!;
    [JsonPropertyName("status")]
    public int Status { get; set; }
    [JsonPropertyName("headers")]
    public Dictionary<string, string> Headers { get; set; } = null!;
    [JsonPropertyName("response")]
    public string Response { get; set; } = null!;
    [JsonPropertyName("cookies")]
    public List<Cookie> Cookies { get; set; } = null!;
    [JsonPropertyName("userAgent")]
    public string UserAgent { get; set; } = null!;
}