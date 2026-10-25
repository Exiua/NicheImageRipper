using System.Net;
using System.Text;

namespace ImpersonateClient;

public sealed class ImpersonateHttpResponseMessage : IDisposable
{
    public HttpStatusCode StatusCode { get; init; }

    public string? ReasonPhrase { get; init; }

    public required Uri RequestUri { get; init; }

    public Version? Version { get; init; }

    public ImpersonateHttpHeaders Headers { get; init; } = new();

    public HttpContent Content { get; init; } = new ByteArrayContent([]);

    public bool IsSuccessStatusCode => StatusCode is >= HttpStatusCode.OK and <= (HttpStatusCode)299;

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

    public override string ToString()
    {
        var builder = new StringBuilder();

        builder.Append("StatusCode: ");
        builder.Append(StatusCode);

        builder.Append(", ReasonPhrase: '");
        builder.Append(ReasonPhrase ?? string.Empty);
        builder.Append('\'');

        builder.Append(", Version: ");
        builder.Append(Version);

        builder.Append(", Content: ");
        builder.Append(Content?.GetType().FullName ?? "<null>");

        builder.AppendLine(", Headers:");
        builder.AppendLine("{");

        AppendHeaders(builder, Headers.All);

        builder.Append('}');

        return builder.ToString();
    }

    private static void AppendHeaders(
        StringBuilder builder,
        IReadOnlyDictionary<string, IReadOnlyList<string>> headers)
    {
        foreach (var header in headers)
        {
            builder.Append("  ");
            builder.Append(header.Key);
            builder.Append(": ");
            builder.AppendLine(string.Join(", ", header.Value));
        }
    }
}