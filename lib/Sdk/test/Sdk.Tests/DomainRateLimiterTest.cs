using System.Diagnostics;
using NicheImageRipper.Sdk.SiteParsing;

namespace NicheImageRipper.Sdk.Tests;

/// <summary>Discovered by <see cref="DomainRateLimiter.Refresh"/> because this assembly is loaded.</summary>
public sealed class TestRateLimitRule : IRateLimitRule
{
    public const string Domain = "ratelimit.test";
    public static readonly TimeSpan Window = TimeSpan.FromMilliseconds(300);

    public IReadOnlyList<string> Domains => [Domain];
    public int MaxRequests => 2;
    public TimeSpan Interval => Window;
}

public class DomainRateLimiterTest
{
    public DomainRateLimiterTest()
    {
        DomainRateLimiter.Refresh(); // fresh budget per test
    }

    [Fact]
    public async Task RequestsBeyondTheBudgetWaitForTheWindow()
    {
        var stopwatch = Stopwatch.StartNew();
        // 2 per window: starts at ~0, 0, 1w, 1w, 2w
        for (var i = 0; i < 5; i++)
        {
            await DomainRateLimiter.WaitAsync($"https://{TestRateLimitRule.Domain}/{i}",
                TestContext.Current.CancellationToken);
        }

        Assert.True(stopwatch.Elapsed >= TestRateLimitRule.Window * 2 - TimeSpan.FromMilliseconds(20),
            $"Elapsed {stopwatch.Elapsed}");
    }

    [Fact]
    public async Task SubdomainsShareTheRulesBudget()
    {
        await DomainRateLimiter.WaitAsync($"https://{TestRateLimitRule.Domain}/", TestContext.Current.CancellationToken);
        await DomainRateLimiter.WaitAsync($"https://cdn.{TestRateLimitRule.Domain}/", TestContext.Current.CancellationToken);

        var stopwatch = Stopwatch.StartNew();
        await DomainRateLimiter.WaitAsync($"https://api.{TestRateLimitRule.Domain}/", TestContext.Current.CancellationToken);

        Assert.True(stopwatch.Elapsed >= TestRateLimitRule.Window - TimeSpan.FromMilliseconds(20),
            $"Elapsed {stopwatch.Elapsed}");
    }

    [Theory]
    [InlineData("https://unrelated.test/")]
    [InlineData("https://notratelimit.test/")] // suffix match is label-aware
    [InlineData("not a url")]
    public async Task UnmatchedUrlsAreNotThrottled(string url)
    {
        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < 10; i++)
        {
            await DomainRateLimiter.WaitAsync(url, TestContext.Current.CancellationToken);
        }

        Assert.True(stopwatch.Elapsed < TestRateLimitRule.Window, $"Elapsed {stopwatch.Elapsed}");
    }

    [Fact]
    public async Task WaitingCanBeCancelled()
    {
        var url = $"https://{TestRateLimitRule.Domain}/";
        await DomainRateLimiter.WaitAsync(url, TestContext.Current.CancellationToken);
        await DomainRateLimiter.WaitAsync(url, TestContext.Current.CancellationToken);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => DomainRateLimiter.WaitAsync(url, cts.Token));
    }
}
