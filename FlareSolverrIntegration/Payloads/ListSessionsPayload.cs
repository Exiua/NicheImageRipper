using System.Text.Json.Serialization;

namespace FlareSolverrIntegration.Payloads;

public class ListSessionsPayload : CommandPayload
{
    [JsonPropertyName("cmd")]
    public override string Command { get; set; } = "sessions.list";
}