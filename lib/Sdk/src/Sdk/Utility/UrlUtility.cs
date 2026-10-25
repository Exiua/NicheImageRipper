using System.Text.RegularExpressions;
using Microsoft.Extensions.Primitives;
using NicheImageRipper.Sdk.Exceptions;
using Serilog;

namespace NicheImageRipper.Sdk.Utility;

public static partial class UrlUtility
{
    private static readonly ILogger Logger = Log.ForContext(typeof(UrlUtility));
    
    public static string ExtractUrl(string url)
    {
        url = url.Replace("</a>", "");
        if (url.Contains("drive.google.com"))
        {
            return GDriveLinkParse(url);
        }

        if (url.Contains("mega.nz"))
        {
            return MegaLinkParse(url);
        }

        var start = url.IndexOf("https:", StringComparison.Ordinal);
        return start != -1 ? url[start..] : url;
    }

    private static string GDriveLinkParse(string url)
    {
        var start = url.IndexOf("https:", StringComparison.Ordinal);
        if (start == -1)
        {
            return "";
        }

        var m = GDriveLinkRegex1().Match(url);
        if (m.Success)
        {
            int end;
            switch (m.Groups[1].Value)
            {
                case "?usp=sharing":
                    end = m.Groups[1].Index + "?usp=sharing".Length;
                    break;
                case "?usp=share_link":
                    end = m.Groups[1].Index + "?usp=share_link".Length;
                    break;
                case "?id=":
                    end = m.Groups[1].Index + "?id=".Length + 33;
                    break;
                default:
                    Logger.Warning("Incorrect Match: {Url}", url);
                    return "";
            }

            return url.Length < end ? "" : url[start..end];
        }

        m = GDriveLinkRegex2().Match(url);
        if (m.Success)
        {
            int end;
            switch (m.Groups[1].Value)
            {
                case "/folders/":
                    end = m.Groups[1].Index + "/folders/".Length + 33;
                    break;
                case "/file/d/":
                    end = m.Groups[1].Index + "/file/d/".Length + 33;
                    break;
                default:
                    Logger.Warning("Incorrect Match: {Url}", url);
                    return "";
            }

            return url.Length < end ? "" : url[start..end];
        }

        Logger.Warning("Unrecognized GDrive url: {Url}", url);
        return "";
    }

    private static string MegaLinkParse(string url)
    {
        var start = url.IndexOf("https:", StringComparison.Ordinal);
        if (start == -1)
        {
            return "";
        }

        var m = MegaLinkRegex().Match(url);
        if (!m.Success)
        {
            Logger.Warning("Unrecognized Mega url: {Url}", url);
            return "";
        }

        int end;
        switch (m.Groups[1].Value)
        {
            case "/folder/":
                end = m.Groups[1].Index + "/folder/".Length + 31;
                break;
            case "/#F!":
                end = m.Groups[1].Index + "/#F!".Length + 31;
                break;
            case "/#!":
                end = m.Groups[1].Index + "/#!".Length + 52;
                break;
            case "/file/":
                end = m.Groups[1].Index + "/file/".Length + 52;
                break;
            default:
                Logger.Warning("Incorrect Match: {Url}", url);
                return "";
        }

        return url.Length < end ? "" : url[start..end];
    }
    
    public static string TruncateLongUrl(string url)
    {
        return string.Concat(url.AsSpan(0, 50), "...", url.AsSpan(url.Length - 49));
    }
    
    public static OrderedDictionary<string, StringValues>? ParseUrlQueryParameters(string url)
    {
        var uri = new Uri(url);
        var query = uri.Query;

        if (query.Length <= 1)
        {
            return null;
        }

        var parameters = new OrderedDictionary<string, StringValues>();

        foreach (var part in query[1..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var keyValue = part.Split('=', 2);

            var key = Uri.UnescapeDataString(keyValue[0].Replace("+", " "));
            var value = keyValue.Length == 2
                ? Uri.UnescapeDataString(keyValue[1].Replace("+", " "))
                : "";

            // More likely to see single values per key, so unlikely to take the Concat path, thus the price of Concat
            // is avoided in most cases.
            if (parameters.TryGetValue(key, out var existing))
            {
                parameters[key] = StringValues.Concat(existing, value);
            }
            else
            {
                parameters[key] = new StringValues(value);
            }
        }

        return parameters;
    }

    public static string GetUrlParameterValue(string url, string parameter)
    {
        return url.Split($"{parameter}=")[1].Split("&")[0];
    }
    
    public static string NormalizeUrl(string url, bool strict = true, params string[] parametersToKeep)
    {
        var query = ParseUrlQueryParameters(url);
        if (query is null)
        {
            if (strict && parametersToKeep.Length > 0)
            {
                throw new RipperException($"Unexpected URL format: {url}; Missing query parameters: {string.Join(", ", parametersToKeep)}");
            }

            return url;
        }

        var kept = new Dictionary<string, string?>();
        foreach (var param in parametersToKeep)
        {
            if (query.TryGetValue(param, out var value))
            {
                kept[param] = value.ToString();
            }
            else if (strict)
            {
                throw new RipperException($"Unexpected URL format: {url}; Missing parameter: {param}");
            }
        }

        var baseUrl = new Uri(url).GetLeftPart(UriPartial.Path);
        var newUrl = baseUrl + (kept.Count > 0
            ? "?" + string.Join("&", kept.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value ?? "")}"))
            : "");
        return newUrl;
    }
    
    [GeneratedRegex(@"(\?usp=sharing|\?usp=share_link|\?id=)")]
    private static partial Regex GDriveLinkRegex1();

    [GeneratedRegex(@"(/folders/|/file/d/)")]
    private static partial Regex GDriveLinkRegex2();

    [GeneratedRegex(@"(/folder/|/#F!|/#!|/file/)")]
    private static partial Regex MegaLinkRegex();
}