using System.Text.Json.Serialization;
using MangaDexLibrary.DataStructures;

namespace MangaDexLibrary.Responses;

public class ErrorResponse : MangaDexResponse
{
    [JsonPropertyName("errors")]
    public MangaDexApiError[] Errors { get; set; } = null!;
}