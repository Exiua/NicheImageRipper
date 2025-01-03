namespace MangaDexLibrary.DataStructures;

public class MangaDexApiError
{
    public Guid Id { get; set; }
    public int Status { get; set; }
    public string Title { get; set; }
    public string Detail { get; set; }
    public object Context { get; set; } // TODO: Figure out what type this is
}