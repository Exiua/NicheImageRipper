using System.Text.Json.Serialization;

namespace MangaDexLibrary.DataStructures;

public class MangaVolume
{
    [JsonPropertyName("volume")]
    public string Volume { get; set; } = null!;
    [JsonPropertyName("count")]
    public int Count { get; set; }
    [JsonPropertyName("chapters")]
    public Dictionary<string, MangaChapter> Chapters { get; set; } = null!;
}