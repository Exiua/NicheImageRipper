using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Driver;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Exceptions;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.PartialSaves;
using NicheImageRipper.Core.Utility;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers;

public abstract class TimeSensitiveHtmlParser : HtmlParser
{
    protected abstract string ImageLinksFileName { get; }
    protected abstract int MaxEntriesPerBatch { get; }
    protected abstract string ParserKey { get; }

    protected TimeSensitiveHtmlParser(WebDriver driver, ApiClientManager clientManager,
                                      Dictionary<string, string> requestHeaders,
                                      FilenameScheme filenameScheme = FilenameScheme.Original)
        : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Refreshes a batch of expired links using the image-links map cached during the original <c>Parse()</c>
    ///     call, resuming from <paramref name="start"/>. Should never be called before <c>Parse()</c> has run at
    ///     least once for <paramref name="ripUrl"/>.
    /// </summary>
    /// <param name="links">The current (possibly-expired) links to refresh.</param>
    /// <param name="start">Index to resume refreshing from.</param>
    /// <param name="ripUrl">
    ///     The original rip URL, used to look up this parser's cached last-fetched-URL. Passed explicitly rather
    ///     than read from this instance's own <c>GivenUrl</c>, since link refreshes run against a freshly
    ///     constructed parser instance that never went through <c>ParseSite</c>.
    /// </param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>The updated list of links.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Parse() has not been run for this URL, or its cached data is missing/unreadable.</exception>
    /// <exception cref="start"><paramref name="start"/> is out of range for the cached image links.</exception>
    public async Task<List<FileLink>> UpdateLinks(List<FileLink> links, int start, string ripUrl,
                                                  CancellationToken cancellationToken = default)
    {
        if (!File.Exists(ImageLinksFileName))
        {
            throw new RipperException("Image links file does not exist. Please run Parse() first.");
        }

        var imageLinksMap = JsonUtility.Deserialize<Dictionary<string, List<string>>>(ImageLinksFileName);
        var lastUrl = TimeSensitiveParserStateManager.Instance.GetLastUrl(ParserKey, ripUrl);
        if (lastUrl is null)
        {
            throw new RipperException("No last URL found. Please run Parse() first.");
        }

        if (imageLinksMap is null || !imageLinksMap.TryGetValue(lastUrl, out var imageLinks))
        {
            throw new RipperException("Image links not found in the file. Please run Parse() first.");
        }

        if (start < 0 || start >= imageLinks.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(start), "Start index is out of range.");
        }

        Logger.Information("Updating links from link {start} of {count}", start + 1, imageLinks.Count);
        var updated = 0;
        foreach (var (i, link) in imageLinks.Enumerate())
        {
            if (i < start)
            {
                continue;
            }

            if (i >= start + MaxEntriesPerBatch)
            {
                break;
            }

            var updatedLink = await UpdateLink(link, cancellationToken);
            // Expired links will have valid filenames, while invalid links will not have filenames
            var regenFilename = links[i].IsInvalid;
            Logger.Information("Updating image link {i} of {count}", i + 1, links.Count);
            links[i].Url = updatedLink;
            if (regenFilename)
            {
                links[i].RegenerateFilename(FilenameScheme, i);
            }

            updated++;
        }

        Logger.Information("Updated {count} image links", updated);
        return links;
    }

    protected abstract Task<string> UpdateLink(string link, CancellationToken cancellationToken = default);

    /// <summary>Records the given URL as the last one fetched, keyed against this rip's <c>GivenUrl</c>, for a later <c>UpdateLinks</c> call to find.</summary>
    protected void StoreLastLink(string url)
    {
        TimeSensitiveParserStateManager.Instance.StoreLastUrl(ParserKey, GivenUrl, url);
    }
}