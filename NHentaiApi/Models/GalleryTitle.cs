using System.Text.Json.Serialization;

namespace NHentaiApi.Models;

public class GalleryTitle
{
    [JsonPropertyName("english")]
    public string? English { get; set; }

    [JsonPropertyName("japanese")]
    public string? Japanese { get; set; }

    [JsonPropertyName("pretty")]
    public string? Pretty { get; set; }
}