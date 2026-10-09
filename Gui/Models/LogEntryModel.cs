using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NicheImageRipper.Gui.Models;

public sealed class LogEntryModel
{
    public DateTimeOffset? Timestamp { get; set; }

    public string? Level { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("MessageTemplate")]
    public string? MessageTemplate { get; set; }

    public string? Exception { get; set; }
    
    public Dictionary<string, object>? Properties { get; set; }
}