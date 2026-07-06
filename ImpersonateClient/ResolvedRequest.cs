namespace ImpersonateClient;

internal sealed class ResolvedRequest
{
    public required string Url { get; init; }
    public required string Method { get; init; }
    public required IReadOnlyList<string> Headers { get; init; }
    public byte[]? Body { get; init; }

    public Browser? Browser { get; init; }
    public string? Ja3 { get; init; }
    public string? Akamai { get; init; }
    public bool PermuteExtensions { get; init; }
    public bool DefaultHeaders { get; init; }
    public bool FollowRedirects { get; init; }
    public bool Verify { get; init; }
    public TimeSpan? Timeout { get; init; }
    public string? Proxy { get; init; }
    public (string User, string Password)? Auth { get; init; }
}