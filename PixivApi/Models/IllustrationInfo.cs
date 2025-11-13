using System.Text.Json.Serialization;

namespace PixivApi.Models;

public class IllustrationInfo : PixivModel
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    [JsonPropertyName("title")]
    public string Title { get; set; } = null!;
    [JsonPropertyName("type")]
    public string Type { get; set; } = null!;
    [JsonPropertyName("image_urls")]
    public ImageUrls ImageUrls { get; set; } = null!;
    [JsonPropertyName("caption")]
    public string Caption { get; set; } = null!;
    [JsonPropertyName("restrict")]
    public int Restrict { get; set; }
    [JsonPropertyName("user")]
    public UserInfo User { get; set; } = null!;
    [JsonPropertyName("tags")]
    public List<IllustrationTag> Tags { get; set; } = null!;
    [JsonPropertyName("tools")]
    public List<string> Tools { get; set; } = null!;
    [JsonPropertyName("create_date")]
    public string CreateDate { get; set; } = null!;
    [JsonPropertyName("page_count")]
    public int PageCount { get; set; }
    [JsonPropertyName("width")]
    public int Width { get; set; }
    [JsonPropertyName("height")]
    public int Height { get; set; }
    [JsonPropertyName("sanity_level")]
    public int SanityLevel { get; set; }
    [JsonPropertyName("x_restrict")]
    public int XRestrict { get; set; }
    [JsonPropertyName("series")]
    public Series? Series { get; set; }
    [JsonPropertyName("meta_single_page")]
    public MetaSinglePage MetaSinglePage { get; set; } = null!;
    [JsonPropertyName("meta_pages")]
    public List<MetaPage> MetaPages { get; set; } = null!;
    [JsonPropertyName("total_view")]
    public int TotalView { get; set; }
    [JsonPropertyName("total_bookmarks")]
    public int TotalBookmarks { get; set; }
    [JsonPropertyName("is_bookmarked")]
    public bool IsBookmarked { get; set; }
    [JsonPropertyName("visible")]
    public bool Visible { get; set; }
    [JsonPropertyName("is_muted")]
    public bool IsMuted { get; set; }
    [JsonPropertyName("illust_ai_type")]
    public int IllustAiType { get; set; }
    [JsonPropertyName("illust_book_style")]
    public int IllustBookStyle { get; set; }
    [JsonPropertyName("total_comments")]
    public int? TotalComments { get; set; }
    [JsonPropertyName("restriction_attributes")]
    public List<string> RestrictionAttributes { get; set; } = [];
}
