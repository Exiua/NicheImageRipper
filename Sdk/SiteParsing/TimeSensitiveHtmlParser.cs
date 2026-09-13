using NicheImageRipper.Sdk.Common.ExtensionMethods;
using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Driver;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.Exceptions;

namespace NicheImageRipper.Sdk.SiteParsing;

public abstract class TimeSensitiveHtmlParser : HtmlParser
{
    protected static ITimeSensitiveParserStateManager TimeSensitiveParserStateManager => Managers.TimeSensitiveParserStateManager.Instance;

    protected abstract int MaxEntriesPerBatch { get; }
    protected abstract string ParserKey { get; }

    protected TimeSensitiveHtmlParser(WebDriver driver,
                                      Dictionary<string, string> requestHeaders,
                                      FilenameScheme filenameScheme = FilenameScheme.Original)
        : base(driver, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Refreshes a batch of expired links using the link list cached during the original <c>Parse()</c>
    ///     call, resuming from <paramref name="start"/>.
    /// </summary>
    public async Task<List<FileLink>> UpdateLinks(List<FileLink> links, int start, string ripUrl,
                                                  CancellationToken cancellationToken = default)
    {
        var imageLinks = TimeSensitiveParserStateManager.GetLinks(ParserKey, ripUrl);
        if (imageLinks is null)
        {
            throw new RipperException("No cached links found. Please run Parse() first.");
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
}