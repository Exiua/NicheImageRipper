using System.Text.Json.Serialization;
using MangaDexLibrary.DataStructures;

namespace MangaDexLibrary.Responses;

public class AggregateMangaResponse : MangaDexResponse
{
    [JsonPropertyName("volumes")]
    public Dictionary<string, MangaVolume> Volumes { get; set; } = null!;
}