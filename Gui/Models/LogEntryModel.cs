using System;
using System.Text.Json.Serialization;

namespace Gui.Models;

public sealed class LogEntryModel
{
    [JsonPropertyName("Timestamp")]
    public DateTimeOffset? Timestamp { get; set; }

    [JsonPropertyName("Level")]
    public string? Level { get; set; }

    [JsonPropertyName("RenderedMessage")]
    public string? RenderedMessage { get; set; }

    [JsonPropertyName("MessageTemplate")]
    public string? MessageTemplate { get; set; }

    [JsonPropertyName("Exception")]
    public string? Exception { get; set; }
}