using System.Text.Json.Serialization;

namespace FlareSolverrClient.Payloads;

public class CreateSessionPayload : CommandPayload
{
    [JsonPropertyName("cmd")]
    public override string Command { get; set; } = "sessions.create";
    
    [JsonPropertyName("session")]
    public string? Session { get; set; }
    
    [JsonPropertyName("proxy")]
    public Dictionary<string, string>? Proxy { get; set; }
}