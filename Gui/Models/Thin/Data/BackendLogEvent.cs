using System.Text.Json.Serialization;

namespace Gui.Models.Thin.Data;

public sealed class BackendLogEvent
{
    [JsonPropertyName("Timestamp")]
    public string? Timestamp { get; set; }

    [JsonPropertyName("Level")]
    public string? Level { get; set; }

    [JsonPropertyName("RenderedMessage")]
    public string? RenderedMessage { get; set; }

    [JsonPropertyName("MessageTemplate")]
    public string? MessageTemplate { get; set; }

    [JsonPropertyName("Exception")]
    public string? Exception { get; set; }
}