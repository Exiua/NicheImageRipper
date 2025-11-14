using System.Text.Json.Serialization;

namespace CSWebDriverClient.Models.Requests;

public class GetPageRequest
{
    [JsonPropertyName("url")]
    public string Url { get; set; }
    [JsonPropertyName("timeout")]
    public double Timeout { get; set; }
    [JsonPropertyName("cookies")]
    public Dictionary<string, string>? Cookies { get; set; }

    public GetPageRequest(string url, double timeout = 5.0, Dictionary<string, string>? cookies = null)
    {
        Url = url;
        Timeout = timeout;
        Cookies = cookies;
    }
}