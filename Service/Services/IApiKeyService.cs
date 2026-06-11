using Service.Models;

namespace Service.Services;

public interface IApiKeyService
{
    Task<CreateApiKeyResult> CreateAsync(string name, string? ownerId, DateTime? expiresUtc, CancellationToken cancellationToken = default);
    Task<ApiKeyValidationResult?> ValidateAsync(string rawApiKey, CancellationToken cancellationToken = default);
    Task<bool> RevokeAsync(Guid id, CancellationToken cancellationToken = default);
}