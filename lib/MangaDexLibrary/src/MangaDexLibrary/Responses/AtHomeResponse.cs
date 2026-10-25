using System.Text.Json.Serialization;
using MangaDexLibrary.DataStructures;

namespace MangaDexLibrary.Responses;

public class AtHomeResponse : MangaDexResponse
{
  [JsonPropertyName("baseUrl")]
  public string BaseUrl { get; set; } = null!;
  [JsonPropertyName("chapter")]
  public ChapterData Chapter { get; set; } = null!;
}
