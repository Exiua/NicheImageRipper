using System.Text;
using System.Text.Json;

namespace ImpersonateClient;

public sealed class ImpersonateHttpResponseMessage : IDisposable
{
    public int StatusCode { get; init; }

    public string? ReasonPhrase { get; init; }

    public required Uri RequestUri { get; init; }

    public Version? Version { get; init; }

    public ImpersonateHttpHeaders Headers { get; init; } = new();

    public byte[] Content { get; init; } = [];

    public bool IsSuccessStatusCode => StatusCode is >= 200 and <= 299;

    public string ReadAsString(Encoding? encoding = null)
    {
        encoding ??= DetectEncoding() ?? Encoding.UTF8;
        return encoding.GetString(Content);
    }

    public T? ReadFromJson<T>(JsonSerializerOptions? options = null)
    {
        return JsonSerializer.Deserialize<T>(Content, options);
    }

    public void EnsureSuccessStatusCode()
    {
        if (!IsSuccessStatusCode)
        {
            throw new ImpersonateHttpRequestException(
                $"Response status code does not indicate success: {StatusCode} {ReasonPhrase}");
        }
    }

    public void Dispose()
    {
    }

    private Encoding? DetectEncoding()
    {
        var contentType = Headers.GetFirstOrDefault("Content-Type");

        if (contentType is null)
        {
            return null;
        }

        const string charsetMarker = "charset=";
        var index = contentType.IndexOf(charsetMarker, StringComparison.OrdinalIgnoreCase);

        if (index < 0)
        {
            return null;
        }

        var charset = contentType[(index + charsetMarker.Length)..].Trim().Trim('"');

        try
        {
            return Encoding.GetEncoding(charset);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}