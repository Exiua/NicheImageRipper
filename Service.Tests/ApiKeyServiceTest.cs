using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NicheImageRipper.Service.Contexts;
using NicheImageRipper.Service.Services;

namespace NicheImageRipper.Service.Tests;

/// <summary>Runs against an in-memory SQLite database, the same provider the service uses.</summary>
public sealed class ApiKeyServiceTest : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly ApplicationDbContext _context;
    private readonly ApiKeyService _service;

    public ApiKeyServiceTest()
    {
        _connection.Open();
        _context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                                            .UseSqlite(_connection)
                                            .Options);
        _context.Database.EnsureCreated();
        _service = new ApiKeyService(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CreatedKeyValidates()
    {
        var created = await _service.CreateAsync("gui", "owner-1", null, Ct);

        var result = await _service.ValidateAsync(created.RawKey, Ct);

        Assert.NotNull(result);
        Assert.Equal(created.Id, result.Id);
        Assert.Equal("gui", result.Name);
        Assert.Equal("owner-1", result.OwnerId);
        Assert.StartsWith(created.Prefix + ".", created.RawKey);
    }

    [Fact]
    public async Task OnlyTheHashIsStored()
    {
        var created = await _service.CreateAsync("gui", null, null, Ct);

        var stored = await _context.ApiKeys.SingleAsync(key => key.Id == created.Id, Ct);

        Assert.Equal(ApiKeyHasher.Hash(created.RawKey), stored.KeyHash);
        Assert.DoesNotContain(created.RawKey.Split('.')[1], stored.KeyHash);
    }

    [Fact]
    public async Task ValidationRecordsLastUse()
    {
        var created = await _service.CreateAsync("gui", null, null, Ct);

        await _service.ValidateAsync(created.RawKey, Ct);

        var stored = await _context.ApiKeys.SingleAsync(key => key.Id == created.Id, Ct);
        Assert.NotNull(stored.LastUsedUtc);
    }

    [Fact]
    public async Task WrongSecretIsRejected()
    {
        var created = await _service.CreateAsync("gui", null, null, Ct);

        Assert.Null(await _service.ValidateAsync(created.Prefix + ".not-the-secret", Ct));
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-dot-at-all")]
    [InlineData(".starts-with-a-dot")]
    [InlineData("ak_000000000000.unknown-prefix")]
    public async Task MalformedOrUnknownKeysAreRejected(string rawKey)
    {
        await _service.CreateAsync("gui", null, null, Ct);

        Assert.Null(await _service.ValidateAsync(rawKey, Ct));
    }

    [Fact]
    public async Task RevokedKeyIsRejected()
    {
        var created = await _service.CreateAsync("gui", null, null, Ct);

        Assert.True(await _service.RevokeAsync(created.Id, Ct));
        Assert.Null(await _service.ValidateAsync(created.RawKey, Ct));
    }

    [Fact]
    public async Task RevokingAnUnknownKeyReportsFalse()
    {
        Assert.False(await _service.RevokeAsync(Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task ExpiredKeyIsRejected()
    {
        var created = await _service.CreateAsync("gui", null, DateTime.UtcNow.AddMinutes(-1), Ct);

        Assert.Null(await _service.ValidateAsync(created.RawKey, Ct));
    }

    [Fact]
    public async Task KeyBeforeExpiryValidates()
    {
        var created = await _service.CreateAsync("gui", null, DateTime.UtcNow.AddDays(1), Ct);

        Assert.NotNull(await _service.ValidateAsync(created.RawKey, Ct));
    }
}

public class ApiKeyGeneratorTest
{
    [Fact]
    public void KeyIsPrefixDotSecret()
    {
        var (prefix, fullKey) = ApiKeyGenerator.Generate();

        Assert.Matches("^ak_[0-9A-F]{12}$", prefix);
        Assert.StartsWith(prefix + ".", fullKey);
        // Safe to put in a header or URL as is
        Assert.Matches("^[A-Za-z0-9]+$", fullKey[(prefix.Length + 1)..]);
    }

    [Fact]
    public void KeysAreUnique()
    {
        var keys = Enumerable.Range(0, 50).Select(_ => ApiKeyGenerator.Generate().FullKey).ToList();

        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    [Fact]
    public void HashIsStableSha256Hex()
    {
        Assert.Equal(ApiKeyHasher.Hash("abc"), ApiKeyHasher.Hash("abc"));
        Assert.NotEqual(ApiKeyHasher.Hash("abc"), ApiKeyHasher.Hash("abd"));
        Assert.Equal("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD", ApiKeyHasher.Hash("abc"));
    }
}
