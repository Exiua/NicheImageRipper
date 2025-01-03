using System.Text.Json.Serialization;

namespace MangaDexLibrary.Responses;

public abstract class MangaDexResponse
{
    [JsonPropertyName("result")]
    public string Result { get; set; }
}