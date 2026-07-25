using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Exceptions;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.Utility;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers;

public abstract class TimeSensitiveHtmlParser : HtmlParser
{
    // Quick fix for updating links, should be replaced with a more robust solution
    private static readonly Dictionary<string, string> LastUrls = new();

    protected abstract string ImageLinksFileName { get; }
    protected abstract int MaxEntriesPerBatch { get; }
    protected abstract string ParserKey { get; }

    protected TimeSensitiveHtmlParser(WebDriver driver, ApiClientManager clientManager,
                                      Dictionary<string, string> requestHeaders,
                                      FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver,
        clientManager, requestHeaders, filenameScheme)
    {
    }

    // Should never be called before Parse() is called
    public async Task<List<FileLink>> UpdateLinks(List<FileLink> links, int start,
                                                  CancellationToken cancellationToken = default)
    {
        if (!File.Exists(ImageLinksFileName))
        {
            throw new RipperException("Image links file does not exist. Please run Parse() first.");
        }

        var imageLinksMap = JsonUtility.Deserialize<Dictionary<string, List<string>>>(ImageLinksFileName);
        if (!LastUrls.TryGetValue(ParserKey, out var lastUrl))
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

    protected void StoreLastLink(string url)
    {
        LastUrls[ParserKey] = url;
    }
}