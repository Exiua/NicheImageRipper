using System.Text.Json.Serialization;

namespace MangaDexLibrary.DataStructures;

public class ChapterData
{
    [JsonPropertyName("hash")]
    public string Hash { get; set; } = null!;
    [JsonPropertyName("data")]
    public string[] Data { get; set; } = null!;
    [JsonPropertyName("dataSaver")]
    public string[] DataSaver { get; set; } = null!;
}