using System.Text.Json.Serialization;

namespace PatreonApiClient.Models;

public sealed class PaginationMeta
{
    [JsonPropertyName("cursors")]
    public PaginationCursors? Cursors { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }
}