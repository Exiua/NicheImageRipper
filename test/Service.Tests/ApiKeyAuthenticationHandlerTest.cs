using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NicheImageRipper.Service.Handlers;
using NicheImageRipper.Service.Models;
using NicheImageRipper.Service.Services;

namespace NicheImageRipper.Service.Tests;

public class ApiKeyAuthenticationHandlerTest
{
    private const string ValidKey = "ak_0123456789AB.secret";

    private static async Task<AuthenticateResult> AuthenticateAsync(string? headerValue, string? ownerId = "owner-1")
    {
        var handler = new ApiKeyAuthenticationHandler(new StaticOptionsMonitor(), NullLoggerFactory.Instance,
            UrlEncoder.Default, new FakeApiKeyService(ownerId));
        var context = new DefaultHttpContext();
        if (headerValue is not null)
        {
            context.Request.Headers["X-API-Key"] = headerValue;
        }

        await handler.InitializeAsync(new AuthenticationScheme("ApiKey", null, typeof(ApiKeyAuthenticationHandler)),
            context);
        return await handler.AuthenticateAsync();
    }

    [Fact]
    public async Task NoHeaderLeavesTheRequestAnonymous()
    {
        var result = await AuthenticateAsync(null);

        Assert.True(result.None);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BlankKeyFails(string headerValue)
    {
        var result = await AuthenticateAsync(headerValue);

        Assert.False(result.Succeeded);
        Assert.Equal("Missing API key.", result.Failure?.Message);
    }

    [Fact]
    public async Task InvalidKeyFails()
    {
        var result = await AuthenticateAsync("ak_0123456789AB.wrong");

        Assert.False(result.Succeeded);
        Assert.Equal("Invalid API key.", result.Failure?.Message);
    }

    [Fact]
    public async Task ValidKeySignsInAsTheKey()
    {
        var result = await AuthenticateAsync(ValidKey);

        Assert.True(result.Succeeded);
        var principal = result.Principal!;
        Assert.Equal("ApiKey", result.Ticket!.AuthenticationScheme);
        Assert.Equal(FakeApiKeyService.KeyId.ToString(), principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal("gui", principal.FindFirstValue(ClaimTypes.Name));
        Assert.Equal("api_key", principal.FindFirstValue("auth_type"));
        Assert.Equal("owner-1", principal.FindFirstValue("owner_id"));
    }

    [Fact]
    public async Task KeyWithoutOwnerHasNoOwnerClaim()
    {
        var result = await AuthenticateAsync(ValidKey, ownerId: null);

        Assert.True(result.Succeeded);
        Assert.Null(result.Principal!.FindFirst("owner_id"));
    }

    private sealed class FakeApiKeyService(string? ownerId) : IApiKeyService
    {
        public static readonly Guid KeyId = Guid.NewGuid();

        public Task<ApiKeyValidationResult?> ValidateAsync(string rawApiKey,
                                                           CancellationToken cancellationToken = default) =>
            Task.FromResult(rawApiKey == ValidKey
                ? new ApiKeyValidationResult { Id = KeyId, Name = "gui", OwnerId = ownerId }
                : null);

        public Task<CreateApiKeyResult> CreateAsync(string name, string? ownerId, DateTime? expiresUtc,
                                                    CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> RevokeAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class StaticOptionsMonitor : IOptionsMonitor<AuthenticationSchemeOptions>
    {
        public AuthenticationSchemeOptions CurrentValue { get; } = new();

        public AuthenticationSchemeOptions Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<AuthenticationSchemeOptions, string?> listener) => null;
    }
}
