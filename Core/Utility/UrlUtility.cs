using System.Text.RegularExpressions;
using NicheImageRipper.Core.SiteParsing;
using Sdk.Exceptions;
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

    /// <summary>
    ///     Extracts the site-check domain and referer for a URL, applying the known per-site referer overrides.
    ///     Does not enforce the production site whitelist — see <see cref = "SiteCheck"/> for that.
    /// </summary>
    internal static string ExtractDomainAndSetReferer(string givenUrl, Dictionary<string, string> requestHeaders)
    {
        var domain = new Uri(givenUrl).Host;
        requestHeaders["referer"] = $"https://{domain}/";
        var domainParts = domain.Split('.');

        var labelsToKeep = HtmlParserFactory.SubdomainSignificantSuffixes
                                            .Where(kvp => domain.EndsWith(kvp.Key))
                                            .Select(kvp => kvp.Value)
                                            .DefaultIfEmpty(2)
                                            .Max();
        domain = domainParts[^Math.Min(labelsToKeep, domainParts.Length)];

        var refererOverride = HtmlParserFactory.RefererOverridesBySuffix
                                               .Where(kvp => domain.EndsWith(kvp.Key)) // reuse `domain` (the host) already computed above
                                               .OrderByDescending(kvp => kvp.Key.Length)
                                               .Select(string? (kvp) => kvp.Value)
                                               .FirstOrDefault();

        if (refererOverride is not null)
        {
            requestHeaders["referer"] = refererOverride;
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

    /// <summary>
    /// Applies known per-site URL rewrites needed before site detection/parsing (e.g. hanime member subdomain,
    /// exhentai->e-hentai cookie sharing).
    /// </summary>
    public static string NormalizeUrl(string url)
    {
        foreach (var (from, to) in HtmlParserFactory.UrlReplacements)
        {
            url = url.Replace(from, to);
        }

        return url;
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
}