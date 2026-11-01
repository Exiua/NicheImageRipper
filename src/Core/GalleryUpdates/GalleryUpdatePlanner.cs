using NicheImageRipper.Sdk.Common.ExtensionMethods;
using NicheImageRipper.Sdk.Configuration;
using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Enums;

namespace NicheImageRipper.Core.GalleryUpdates;

/// <summary>
///     Merges a fresh parse of an updatable gallery with what earlier rips stored, deciding each item's final position,
///     which existing files move, and what to download. Pure: touches neither the filesystem nor the database.
/// </summary>
public static class GalleryUpdatePlanner
{
    private sealed class Entry(GalleryItem? stored, ParsedGalleryItem? parsed, bool download)
    {
        public GalleryItem? Stored { get; } = stored;
        public ParsedGalleryItem? Parsed { get; } = parsed;
        public bool Download { get; } = download;
    }

    /// <param name="stored">Items from earlier rips, in gallery order.</param>
    /// <param name="parsed">Items from this parse, in the parser's order.</param>
    /// <param name="strategy">The strategy resolved for the parser (see <see cref="GalleryUpdateSupport"/>).</param>
    /// <param name="placement">The parser's <see cref="Sdk.SiteParsing.IUpdatableHtmlParser.NewItemPlacement"/>.</param>
    /// <param name="mode"><see cref="GalleryUpdateMode.Full"/> downloads every parsed item and forgets earlier ones.</param>
    /// <param name="filenameScheme">With <see cref="FilenameScheme.Chronological"/>, filenames follow positions, so
    ///     existing files are renamed when items are inserted before them.</param>
    /// <param name="directoryName">The directory the gallery is ripped to.</param>
    /// <param name="cursor">The parser's update cursor for this rip.</param>
    public static GalleryUpdatePlan Plan(IReadOnlyList<GalleryItem> stored, IReadOnlyList<ParsedGalleryItem> parsed,
                                         GalleryUpdateStrategy strategy, NewItemPlacement placement,
                                         GalleryUpdateMode mode, FilenameScheme filenameScheme,
                                         string directoryName, string? cursor)
    {
        var uniqueParsed = parsed.DistinctBy(p => p.Key).ToList();
        var entries = mode == GalleryUpdateMode.Full || stored.Count == 0
            ? uniqueParsed.Select(p => new Entry(null, p, true)).ToList()
            : strategy switch
            {
                GalleryUpdateStrategy.NewItemsOnly => MergeNewItemsOnly(stored, uniqueParsed, placement),
                GalleryUpdateStrategy.FullDiff => MergeFullDiff(stored, uniqueParsed),
                _ => throw new ArgumentOutOfRangeException(nameof(strategy), strategy, null),
            };

        var chronological = filenameScheme == FilenameScheme.Chronological;
        var renames = new List<GalleryFileRename>();
        var existingItems = new List<GalleryItem>();
        var pendingItems = new List<PendingGalleryItem>();
        var downloads = new List<FileLink>();
        foreach (var (position, entry) in entries.Enumerate())
        {
            string? existingFilename = null;
            if (entry.Stored is { } item)
            {
                existingFilename = chronological ? position + Path.GetExtension(item.Filename) : item.Filename;
                if (existingFilename != item.Filename)
                {
                    renames.Add(new GalleryFileRename(item.Filename, existingFilename));
                }

                // Until a re-download succeeds the file on disk is still the old version
                var version = entry.Download ? item.Version : entry.Parsed?.Version ?? item.Version;
                existingItems.Add(item with { Version = version, Position = position, Filename = existingFilename });
            }

            if (entry is { Download: true, Parsed: { } parsedItem })
            {
                var link = parsedItem.Link;
                if (chronological)
                {
                    link.Rename(position);
                }

                var replaced = existingFilename is not null && existingFilename != link.Filename ? existingFilename : null;
                pendingItems.Add(new PendingGalleryItem(parsedItem.Key, parsedItem.Version, position, downloads.Count,
                    replaced));
                downloads.Add(link);
            }
        }

        return new GalleryUpdatePlan
        {
            DirectoryName = directoryName,
            Cursor = cursor,
            Renames = renames,
            ExistingItems = existingItems,
            PendingItems = pendingItems,
            Downloads = downloads,
        };
    }

    /// <summary>Existing items stay as they are; unseen items go together as one block at <paramref name="placement"/>.</summary>
    private static List<Entry> MergeNewItemsOnly(IReadOnlyList<GalleryItem> stored, List<ParsedGalleryItem> parsed,
                                                 NewItemPlacement placement)
    {
        var storedKeys = stored.Select(s => s.Key).ToHashSet();
        var existing = stored.Select(s => new Entry(s, null, false));
        var added = parsed.Where(p => !storedKeys.Contains(p.Key)).Select(p => new Entry(null, p, true));
        return placement == NewItemPlacement.Start ? [..added, ..existing] : [..existing, ..added];
    }

    /// <summary>
    ///     Follows the parsed order. Existing items whose version changed are downloaded again (an item stored without
    ///     a version just takes the parsed one); items no longer listed are kept right after the item that preceded
    ///     them before.
    /// </summary>
    private static List<Entry> MergeFullDiff(IReadOnlyList<GalleryItem> stored, List<ParsedGalleryItem> parsed)
    {
        var storedByKey = stored.ToDictionary(s => s.Key);
        var entries = parsed.Select(p => storedByKey.TryGetValue(p.Key, out var s)
            ? new Entry(s, p, p.Version is not null && s.Version is not null && p.Version != s.Version)
            : new Entry(null, p, true)).ToList();

        var parsedKeys = parsed.Select(p => p.Key).ToHashSet();
        for (var i = 0; i < stored.Count; i++)
        {
            if (parsedKeys.Contains(stored[i].Key))
            {
                continue;
            }

            // The previous stored item is always in entries by now: either parsed, or a removed one placed earlier
            var previousKey = i == 0 ? null : stored[i - 1].Key;
            var insertAt = previousKey is null ? 0 : entries.FindIndex(e => e.Stored?.Key == previousKey) + 1;
            entries.Insert(insertAt, new Entry(stored[i], null, false));
        }

        return entries;
    }
}
