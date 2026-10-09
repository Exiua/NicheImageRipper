namespace NicheImageRipper.Service.Models;

public sealed class ApiKeyValidationResult
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? OwnerId { get; set; }
}