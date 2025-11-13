using System.Text.Json.Serialization;

namespace PixivApi.Models;

public class Series : PixivModel
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    [JsonPropertyName("title")]
    public string Title { get; set; } = null!;
}
