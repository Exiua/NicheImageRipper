using System.Text.Json.Serialization;

namespace FlareSolverrClient.Payloads;

public class DeleteSessionPayload : CommandPayload
{
    [JsonPropertyName("cmd")]
    public override string Command { get; set; } = "sessions.destroy";
    
    [JsonPropertyName("session")]
    public string Session { get; set; }
}