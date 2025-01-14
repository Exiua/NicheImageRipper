namespace MangaDexLibrary.DataStructures;

public class MangaDexApiError
{
    public Guid Id { get; set; }
    public int Status { get; set; }
    public string Title { get; set; } = null!;
    public string Detail { get; set; } = null!;
    public object Context { get; set; } = null!; // TODO: Figure out what type this is
}