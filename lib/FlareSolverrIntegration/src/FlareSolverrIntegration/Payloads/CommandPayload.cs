using System.Text.Json.Serialization;

namespace FlareSolverrIntegration.Payloads;

public abstract class CommandPayload
{
    [JsonPropertyName("cmd")]
    public virtual string Command { get; set; } = null!;
}