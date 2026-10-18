namespace MangaDexLibrary.DataStructures;

/// <summary>One entry of an API error response (the <c>Error</c> schema in MangaDex's OpenAPI spec).</summary>
public class MangaDexApiError
{
    public Guid Id { get; set; }
    public int Status { get; set; }
    public string Title { get; set; } = null!;
    public string? Detail { get; set; }
    public string? Context { get; set; }
}
