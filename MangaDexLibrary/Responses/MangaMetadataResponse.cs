using System.Text.Json.Serialization;
using MangaDexLibrary.DataStructures;

namespace MangaDexLibrary.Responses;

public class MangaMetadataResponse : MangaDexResponse
{
    [JsonPropertyName("response")]
    public string Response { get; set; } = null!;
    [JsonPropertyName("data")]
    public MangaMetadata Data { get; set; } = null!;
}