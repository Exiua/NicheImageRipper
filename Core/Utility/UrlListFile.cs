using System.Text.Json;

namespace NicheImageRipper.Core.Utility;

/// <summary>Reads saved URL lists (unfinished rips): a JSON array of absolute http(s) URLs.</summary>
public static class UrlListFile
{
    private static readonly JsonDocumentOptions DocumentOptions = new() { AllowTrailingCommas = true };

    /// <returns>true with the URLs if the file is a valid list; otherwise false with a reason in <paramref name="error"/></returns>
    public static bool TryRead(string path, out List<string> urls, out string error)
    {
        urls = [];
        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            error = $"Couldn't read the file: {e.Message}";
            return false;
        }

        return TryParse(json, out urls, out error);
    }

    /// <inheritdoc cref="TryRead"/>
    public static bool TryParse(string json, out List<string> urls, out string error)
    {
        urls = [];
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, DocumentOptions);
        }
        catch (JsonException e)
        {
            error = $"Not valid JSON: {e.Message}";
            return false;
        }

        var parsed = new List<string>();
        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                error = "Expected a JSON array of URLs";
                return false;
            }

            var index = 0;
            foreach (var item in document.RootElement.EnumerateArray())
            {
                index++;
                if (item.ValueKind != JsonValueKind.String)
                {
                    error = $"Item {index} isn't a string";
                    return false;
                }

                var url = item.GetString()!.Trim();
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    error = $"Item {index} isn't an http(s) URL: {Truncate(url)}";
                    return false;
                }

                parsed.Add(url);
            }
        }

        if (parsed.Count == 0)
        {
            error = "The list is empty";
            return false;
        }

        // Only hand back URLs once the whole file has validated
        urls = parsed;
        error = "";
        return true;
    }

    private static string Truncate(string value) => value.Length <= 80 ? value : value[..77] + "...";
}
