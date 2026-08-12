using System.Text.RegularExpressions;
using NicheImageRipper.Core.Exceptions;
using NicheImageRipper.Core.SiteParsing;
using Serilog;

namespace NicheImageRipper.Core.Utility;

public static partial class UrlUtility
{
    private static readonly ILogger Logger = Log.ForContext(typeof(UrlUtility));
    /// <summary>
    ///     Check the url to make sure it is from valid site
    /// </summary>
    /// <param name = "givenUrl">Url to validate</param>
    /// <returns>True if url is for a supported site</returns>
    public static bool UrlCheck(string givenUrl)
    {
        var parsedUri = new Uri(givenUrl);
        var baseUrl = $"{parsedUri.Scheme}://{parsedUri.Host}/";
        return HtmlParserFactory.SupportedUrls.Contains(baseUrl) || baseUrl.Contains("newgrounds.com");
    }

    private static readonly string[] SpecialDomains = ["inven.co.kr", "danbooru.donmai.us"];
    /// <summary>
    ///     Extracts the site-check domain and referer for a URL, applying the known per-site referer overrides.
    ///     Does not enforce the production site whitelist — see <see cref = "SiteCheck"/> for that.
    /// </summary>
    internal static string ExtractDomainAndSetReferer(string givenUrl, Dictionary<string, string> requestHeaders)
    {
        var domain = new Uri(givenUrl).Host;
        requestHeaders["referer"] = $"https://{domain}/";
        var domainParts = domain.Split('.');
        domain = SpecialDomains.Any(domain.Contains) ? domainParts[^3] : domainParts[^2];
        if (givenUrl.Contains("https://members.hanime.tv/") || givenUrl.Contains("https://hanime.tv/"))
        {
            requestHeaders["referer"] = "https://cdn.discordapp.com/";
        }
        else if (givenUrl.Contains("https://kemono.party/") || givenUrl.Contains("inven.co.kr"))
        {
            requestHeaders["referer"] = "";
        }

        return domain;
    }

    /// <summary>
    ///     Check the site and return the domain and delay between requests. Also sets the referer header
    /// </summary>
    /// <param name = "givenUrl">Url to check</param>
    /// <param name = "requestHeaders">Request headers to modify</param>
    /// <returns>Domain and delay between requests</returns>
    /// <exception cref = "RipperException">If the site is not supported</exception>
    public static (string, float) SiteCheck(string givenUrl, Dictionary<string, string> requestHeaders)
    {
        if (!UrlCheck(givenUrl))
        {
            throw new RipperException("Not a support site");
        }

        var domain = ExtractDomainAndSetReferer(givenUrl, requestHeaders);
        if (givenUrl.Contains("https://e-hentai.org/") || givenUrl.Contains("https://exhentai.org/"))
        {
            return (domain, 2.5f);
        }

        return (domain, 0.2f);
    }

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
                    PrintUtility.Print($"Incorrect Match: {url}");
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
                    PrintUtility.Print(url);
                    return "";
            }

            return url.Length < end ? "" : url[start..end];
        }

        PrintUtility.Print(url);
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
            PrintUtility.Print(url);
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
                PrintUtility.Print($"Incorrect Match: {url}");
                return "";
        }

        return url.Length < end ? "" : url[start..end];
    }

    public static string TruncateLongUrl(string url)
    {
        return string.Concat(url.AsSpan(0, 50), "...", url.AsSpan(url.Length - 49));
    }

    public static string GetUrlParameterValue(string url, string parameter)
    {
        return url.Split($"{parameter}=")[1].Split("&")[0];
    }

    /// <summary>
    /// Applies known per-site URL rewrites needed before site detection/parsing (e.g. hanime member subdomain,
    /// exhentai->e-hentai cookie sharing).
    /// </summary>
    public static string NormalizeUrl(string url)
    {
        return url.Replace("members.", "www.") // Hanime
        .Replace("exhentai.org", "e-hentai.org"); // Need to go through e-hentai first for cookies
    }

    /// <summary>
    ///     Extracts the base URL by trimming everything after the last '/'
    /// </summary>
    /// <param name = "url">The full URL to trim</param>
    /// <returns>The base URL</returns>
    public static string TrimUrl(string url)
    {
        var qs = url.IndexOf('?');
        var queryStart = qs == -1 ? url.Length - 1 : qs;
        Logger.Debug("QueryStart: {QueryStart}, Length: {Length}", queryStart, url.Length);
        return url[..(url.LastIndexOf('/', queryStart) + 1)];
    }

    [GeneratedRegex(@"(\?usp=sharing|\?usp=share_link|\?id=)")]
    private static partial Regex GDriveLinkRegex1();
    [GeneratedRegex(@"(/folders/|/file/d/)")]
    private static partial Regex GDriveLinkRegex2();
    [GeneratedRegex(@"(/folder/|/#F!|/#!|/file/)")]
    private static partial Regex MegaLinkRegex();
}