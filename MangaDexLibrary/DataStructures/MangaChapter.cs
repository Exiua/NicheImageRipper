using System.Text.Json.Serialization;

namespace MangaDexLibrary.DataStructures;

public class MangaChapter
{
    [JsonPropertyName("chapter")]
    public string Chapter { get; set; } = null!;
    [JsonPropertyName("id")]
    public Guid Id { get; set; }
    [JsonPropertyName("others")]
    public Guid[] Others { get; set; } = null!;
    [JsonPropertyName("count")]
    public int Count { get; set; }
}