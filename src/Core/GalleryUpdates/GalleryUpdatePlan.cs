using System.Text.Json.Serialization;
using NicheImageRipper.Sdk.DataStructures;

namespace NicheImageRipper.Core.GalleryUpdates;

/// <summary>An item already ripped from a gallery, at its place in the gallery and its file on disk.</summary>
public sealed record GalleryItem(string Key, string? Version, int Position, string Filename);

/// <summary>An item from the latest parse, identified by the parser's item key/version.</summary>
public sealed record ParsedGalleryItem(string Key, string? Version, FileLink Link);

/// <summary>A file in the gallery's directory that moves to a new name before downloading.</summary>
public sealed record GalleryFileRename(string From, string To);

/// <summary>
///     An item the update downloads: <see cref="DownloadIndex"/> is its index in the rip's
///     <see cref="RipInfo.Urls"/>. <see cref="ReplacedFilename"/> is set when it re-downloads an existing item under a
///     different name, so the old file can be removed once the new one is in place.
/// </summary>
public sealed record PendingGalleryItem(string Key, string? Version, int Position, int DownloadIndex,
                                        string? ReplacedFilename);

/// <summary>
///     How one rip of an updatable gallery turns what's on disk into the parsed gallery. Persisted until the rip
///     succeeds, so an interrupted update resumes from the same plan instead of being planned again.
/// </summary>
public sealed class GalleryUpdatePlan
{
    public required string DirectoryName { get; init; }

    /// <summary>The parser's <see cref="RipInfo.UpdateCursor"/> for this rip, saved once it succeeds.</summary>
    public string? Cursor { get; init; }

    /// <summary>Renames that move existing files to their new positions (chronological naming only).</summary>
    public required List<GalleryFileRename> Renames { get; init; }

    /// <summary>Every item kept from earlier rips, with its position and filename after <see cref="Renames"/>.</summary>
    public required List<GalleryItem> ExistingItems { get; init; }

    public required List<PendingGalleryItem> PendingItems { get; init; }

    /// <summary>Whether <see cref="Renames"/> were done and <see cref="ExistingItems"/> saved.</summary>
    public bool RenamesApplied { get; set; }

    /// <summary>The links to download, in <see cref="PendingGalleryItem.DownloadIndex"/> order. Only set when
    /// planning; a resumed rip gets them from its partial save.</summary>
    [JsonIgnore]
    public List<FileLink> Downloads { get; init; } = [];

    [JsonIgnore]
    public bool HasNothingToDownload => PendingItems.Count == 0;
}
