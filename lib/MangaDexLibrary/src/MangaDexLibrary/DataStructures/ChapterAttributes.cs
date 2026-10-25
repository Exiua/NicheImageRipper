using System.Text.Json.Serialization;

namespace MangaDexLibrary.DataStructures;

public class ChapterAttributes
{
    [JsonPropertyName("volume")]
    public string? Volume { get; set; }
    [JsonPropertyName("chapter")]
    public string? Chapter { get; set; }
    [JsonPropertyName("title")]
    public string? Title { get; set; }
    [JsonPropertyName("translatedLanguage")]
    public string? TranslatedLanguage { get; set; }
    [JsonPropertyName("externalUrl")]
    public string? ExternalUrl { get; set; }
    [JsonPropertyName("publishAt")]
    public DateTime PublishAt { get; set; }
    [JsonPropertyName("readableAt")]
    public DateTime ReadableAt { get; set; }
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }
    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; }
    [JsonPropertyName("pages")]
    public int Pages { get; set; }
    [JsonPropertyName("version")]
    public int Version { get; set; }
}