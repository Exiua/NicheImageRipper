using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NicheImageRipper.Service.Contexts;
using NicheImageRipper.Service.Models;

namespace NicheImageRipper.Service.Services;

public class ApiKeyService(ApplicationDbContext context) : IApiKeyService
{
    public async Task<CreateApiKeyResult> CreateAsync(string name, string? ownerId, DateTime? expiresUtc, CancellationToken cancellationToken = default)
    {
        var (prefix, rawKey) = ApiKeyGenerator.Generate();
        var hash = ApiKeyHasher.Hash(rawKey);

        var entity = new ApiKey
        {
            Id = Guid.NewGuid(),
            Name = name,
            KeyPrefix = prefix,
            KeyHash = hash,
            OwnerId = ownerId,
            CreatedUtc = DateTime.UtcNow,
            ExpiresUtc = expiresUtc,
            IsRevoked = false
        };

        context.ApiKeys.Add(entity);
        await context.SaveChangesAsync();

        return new CreateApiKeyResult
        {
            Id = entity.Id,
            Name = entity.Name,
            Prefix = entity.KeyPrefix,
            RawKey = rawKey
        };
    }

    public async Task<ApiKeyValidationResult?> ValidateAsync(string rawApiKey, CancellationToken cancellationToken = default)
    {
        var dotIndex = rawApiKey.IndexOf('.');
        if (dotIndex <= 0)
        {
            return null;
        }

        var prefix = rawApiKey[..dotIndex];
        var hash = ApiKeyHasher.Hash(rawApiKey);

        var entity = await context.ApiKeys
            .FirstOrDefaultAsync(x => x.KeyPrefix == prefix);

        if (entity == null)
        {
            return null;
        }

        if (entity.IsRevoked)
        {
            return null;
        }

        if (entity.ExpiresUtc.HasValue && entity.ExpiresUtc.Value <= DateTime.UtcNow)
        {
            return null;
        }

        if (!string.Equals(entity.KeyHash, hash, StringComparison.Ordinal))
        {
            return null;
        }

        entity.LastUsedUtc = DateTime.UtcNow;
        await context.SaveChangesAsync();

        return new ApiKeyValidationResult
        {
            Id = entity.Id,
            Name = entity.Name,
            OwnerId = entity.OwnerId
        };
    }

    public async Task<bool> RevokeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await context.ApiKeys.FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null)
        {
            return false;
        }

        entity.IsRevoked = true;
        await context.SaveChangesAsync();
        return true;
    }
}

public static class ApiKeyGenerator
{
    public static (string Prefix, string FullKey) Generate()
    {
        var prefixBytes = RandomNumberGenerator.GetBytes(6);
        var secretBytes = RandomNumberGenerator.GetBytes(32);

        var prefix = "ak_" + Convert.ToHexString(prefixBytes);
        var secret = Convert.ToBase64String(secretBytes)
                            .Replace("+", "")
                            .Replace("/", "")
                            .Replace("=", "");

        var fullKey = $"{prefix}.{secret}";
        return (prefix, fullKey);
    }
}

public static class ApiKeyHasher
{
    public static string Hash(string apiKey)
    {
        var bytes = Encoding.UTF8.GetBytes(apiKey);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}