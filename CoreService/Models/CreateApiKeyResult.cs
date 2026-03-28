namespace CoreService.Models;

public sealed class CreateApiKeyResult
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Prefix { get; set; } = null!;
    public string RawKey { get; set; } = null!;
}