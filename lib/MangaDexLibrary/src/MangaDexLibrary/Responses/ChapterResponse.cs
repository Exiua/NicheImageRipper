using System.Text.Json.Serialization;
using MangaDexLibrary.DataStructures;

namespace MangaDexLibrary.Responses;

public class ChapterResponse : MangaDexResponse
{
    [JsonPropertyName("response")]
    public string Response { get; set; } = null!;
    [JsonPropertyName("data")]
    public ChapterMetadata Data { get; set; } = null!;
}