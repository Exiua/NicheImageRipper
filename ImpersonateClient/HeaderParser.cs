using System.Text;

namespace ImpersonateClient;

internal static class HeaderParser
{
    public static ParsedHeaders Parse(byte[] rawHeaders)
    {
        var text = Encoding.ASCII.GetString(rawHeaders);

        var blocks = text.Split(
            ["\r\n\r\n", "\n\n"],
            StringSplitOptions.RemoveEmptyEntries);

        var lastBlock = blocks.LastOrDefault() ?? string.Empty;

        var lines = lastBlock.Split(
            ["\r\n", "\n"],
            StringSplitOptions.RemoveEmptyEntries);

        var result = new ParsedHeaders();

        foreach (var line in lines)
        {
            if (line.StartsWith("HTTP/", StringComparison.OrdinalIgnoreCase))
            {
                ParseStatusLine(line, result);
                continue;
            }

            var separatorIndex = line.IndexOf(':');

            if (separatorIndex <= 0)
            {
                continue;
            }

            var name = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();

            result.Headers.Add(name, value);
        }

        return result;
    }

    private static void ParseStatusLine(string line, ParsedHeaders result)
    {
        var parts = line.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length >= 1 && parts[0].StartsWith("HTTP/", StringComparison.OrdinalIgnoreCase))
        {
            var versionText = parts[0]["HTTP/".Length..];

            if (Version.TryParse(versionText, out Version? version))
            {
                result.Version = version;
            }
        }

        if (parts.Length >= 2 && int.TryParse(parts[1], out var statusCode))
        {
            result.StatusCode = statusCode;
        }

        if (parts.Length >= 3)
        {
            result.ReasonPhrase = parts[2];
        }
    }
}

internal sealed class ParsedHeaders
{
    public int? StatusCode { get; set; }

    public string? ReasonPhrase { get; set; }

    public Version? Version { get; set; }

    public ImpersonateHttpHeaders Headers { get; } = new();
}