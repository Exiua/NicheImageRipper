using System.Text.Json.Serialization;

namespace MangaDexLibrary.DataStructures;

public class TagAttributes
{
    [JsonPropertyName("name")]
    public Dictionary<string, string> Name { get; set; } = null!;
    [JsonPropertyName("description")]
    public Dictionary<string, string> Description { get; set; } = null!;
    [JsonPropertyName("group")]
    public string Group { get; set; } = null!;
    [JsonPropertyName("version")]
    public int Version { get; set; }
}