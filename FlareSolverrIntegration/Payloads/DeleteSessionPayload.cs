using System.Text.Json.Serialization;

namespace FlareSolverrIntegration.Payloads;

public class DeleteSessionPayload : CommandPayload
{
    [JsonPropertyName("cmd")]
    public override string Command { get; set; } = "sessions.destroy";
    
    [JsonPropertyName("session")]
    public string Session { get; set; }
}