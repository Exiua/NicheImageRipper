using System.ComponentModel.DataAnnotations;

namespace CoreService.Models;

public class ApiKey
{
    [Key]
    public Guid Id { get; set; }

    [MaxLength(200)]
    public string Name { get; set; } = null!;

    [MaxLength(64)]
    public string KeyPrefix { get; set; } = null!;

    [MaxLength(128)]
    public string KeyHash { get; set; } = null!;

    public bool IsRevoked { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime? ExpiresUtc { get; set; }

    public DateTime? LastUsedUtc { get; set; }

    public string? OwnerId { get; set; }
}