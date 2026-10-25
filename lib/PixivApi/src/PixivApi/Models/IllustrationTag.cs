using System.Text.Json.Serialization;

namespace PixivApi.Models;

public class IllustrationTag : PixivModel
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;
    [JsonPropertyName("translated_name")]
    public string? TranslatedName { get; set; }
}