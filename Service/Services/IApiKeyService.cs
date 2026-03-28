using Service.Models;

namespace Service.Services;

public interface IApiKeyService
{
    Task<CreateApiKeyResult> CreateAsync(string name, string? ownerId, DateTime? expiresUtc);
    Task<ApiKeyValidationResult?> ValidateAsync(string rawApiKey);
    Task<bool> RevokeAsync(Guid id);
}