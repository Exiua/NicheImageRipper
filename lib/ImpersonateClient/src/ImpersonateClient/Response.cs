namespace ImpersonateClient;

public sealed record Response(
    long StatusCode,
    byte[] Body,
    IReadOnlyDictionary<string, string> Headers,
    string Url);