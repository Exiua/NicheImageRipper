using System.Text.Json.Serialization;

namespace MangaDexLibrary.DataStructures;

public class MangaAttributes
{
    [JsonPropertyName("title")]
    public Dictionary<string, string> Title { get; set; } = null!;
    [JsonPropertyName("altTitles")]
    public Dictionary<string, string>[] AltTitles { get; set; } = null!;
    [JsonPropertyName("description")]
    public Dictionary<string, string> Description { get; set; } = null!;
    [JsonPropertyName("isLocked")]
    public bool IsLocked { get; set; }
    [JsonPropertyName("links")]
    public Dictionary<string, string> Links { get; set; } = null!;
    [JsonPropertyName("originalLanguage")]
    public string OriginalLanguage { get; set; } = null!;
    [JsonPropertyName("lastVolume")]
    public string LastVolume { get; set; } = null!;
    [JsonPropertyName("lastChapter")]
    public string LastChapter { get; set; } = null!;
    [JsonPropertyName("publicationDemographic")]
    public string? PublicationDemographic { get; set; }
    [JsonPropertyName("status")]
    public string Status { get; set; } = null!;
    [JsonPropertyName("year")]
    public int Year { get; set; }
    [JsonPropertyName("contentRating")]
    public string ContentRating { get; set; } = null!;
    [JsonPropertyName("tags")]
    public MangaTag[] Tags { get; set; } = null!;
    [JsonPropertyName("state")]
    public string State { get; set; } = null!;
    [JsonPropertyName("chapterNumbersResetOnNewVolume")]
    public bool ChapterNumbersResetOnNewVolume { get; set; }
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }
    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; }
    [JsonPropertyName("version")]
    public int Version { get; set; }
    [JsonPropertyName("availableTranslatedLanguages")]
    public string[] AvailableTranslatedLanguages { get; set; } = null!;
    [JsonPropertyName("latestUploadedChapter")]
    public Guid LatestUploadedChapter { get; set; }
}
