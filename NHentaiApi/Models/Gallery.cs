using System.Text.Json.Serialization;

namespace NHentaiApi.Models;

public class Gallery
{
    [JsonPropertyName("id")] public int Id { get; set; }

    [JsonPropertyName("media_id")] public string? MediaId { get; set; }

    [JsonPropertyName("title")] public GalleryTitle? Title { get; set; }

    [JsonPropertyName("cover")] public ImageInfo? Cover { get; set; }

    [JsonPropertyName("thumbnail")] public ImageInfo? Thumbnail { get; set; }

    [JsonPropertyName("scanlator")] public string? Scanlator { get; set; }

    [JsonPropertyName("upload_date")] public long UploadDate { get; set; }

    [JsonPropertyName("tags")] public List<GalleryTag> Tags { get; set; } = [];

    [JsonPropertyName("num_pages")] public int NumPages { get; set; }

    [JsonPropertyName("num_favorites")] public int NumFavorites { get; set; }

    [JsonPropertyName("pages")] public List<GalleryPage> Pages { get; set; } = [];
}
