namespace Sdk.SiteParsing;

/// <summary>
///     Helpers for collecting and persisting links to external sites (e.g. Mega, Drive, Dropbox) found while
///     scraping a page — used by parsers that need to hand off embedded links to another registered parser
///     rather than downloading them directly.
/// </summary>
public static class ExternalLinkExtractor
{
    /// <summary>Every domain a registered <see cref="ParameterizedHtmlParser"/> can be delegated to.</summary>
    public static IReadOnlySet<string> ExternalSites => HtmlParserFactory.DelegatableDomains;

    public static Dictionary<string, List<string>> CreateExternalLinkDict()
    {
        var externalLinks = new Dictionary<string, List<string>>();
        foreach (var site in ExternalSites)
        {
            externalLinks[site] = [];
        }

        return externalLinks;
    }

    public static Dictionary<string, List<string>> ExtractExternalUrls(IEnumerable<string> urls)
    {
        var externalLinks = CreateExternalLinkDict();
        var urlList = urls.ToList();
        foreach (var site in externalLinks.Keys)
        {
            foreach (var link in urlList.Where(url => !string.IsNullOrEmpty(url) && url.Contains(site))
                                        .Select(UrlUtility.ExtractUrl)
                                        .Where(link => link != ""))
            {
                externalLinks[site].Add(link + '\n');
            }
        }

        return externalLinks;
    }

    public static void SaveExternalLinks(Dictionary<string, List<string>> links)
    {
        foreach (var (site, siteLinks) in links)
        {
            if (siteLinks.Count == 0)
            {
                continue;
            }

            File.AppendAllLines($"{site}_links.txt", siteLinks);
        }
    }

    public static bool UrlCanBeParsed(string url)
    {
        return !string.IsNullOrEmpty(url) && ExternalSites.Any(url.Contains);
    }
}