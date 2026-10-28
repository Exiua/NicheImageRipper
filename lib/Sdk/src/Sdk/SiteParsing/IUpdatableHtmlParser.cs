using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Enums;

namespace NicheImageRipper.Sdk.SiteParsing;

/// <summary>
///     Implemented by parsers for galleries that can change after they were ripped (new images added to an album, a
///     creator posting again). Re-ripping such a gallery downloads either everything or only what changed, per
///     <see cref="Configuration.GalleryUpdateMode"/>. The parser only describes its items; working out what changed,
///     renaming existing files and storing state between rips is done by Core.
/// </summary>
/// <remarks>
///     <see cref="HtmlParser.Parse"/> must return the items in their intended final order (e.g. chronological), since
///     <see cref="FilenameScheme.Chronological"/> names come from each item's position in the merged gallery.
/// </remarks>
public interface IUpdatableHtmlParser
{
    /// <summary>Whether past items can change, which decides how an update is merged with what's on disk.</summary>
    GalleryUpdateStrategy UpdateStrategy { get; }

    /// <summary>Where new items go for <see cref="GalleryUpdateStrategy.NewItemsOnly"/>. Ignored for
    /// <see cref="GalleryUpdateStrategy.FullDiff"/>, where items go wherever the parser lists them.</summary>
    NewItemPlacement NewItemPlacement => NewItemPlacement.End;

    /// <summary>
    ///     Set before <see cref="HtmlParser.Parse"/> on an update rip; null on a first or full rip. A
    ///     <see cref="GalleryUpdateStrategy.NewItemsOnly"/> parser may return only the items it hasn't seen (e.g. stop
    ///     paginating at the first known item or at <see cref="GalleryUpdateContext.PreviousCursor"/>); a
    ///     <see cref="GalleryUpdateStrategy.FullDiff"/> parser must still return the whole gallery.
    /// </summary>
    GalleryUpdateContext? UpdateContext { get; set; }

    /// <summary>
    ///     Identity of an item that stays the same across rips, used to tell new items from ones already ripped.
    ///     Override when URLs aren't stable (e.g. signed or expiring CDN links), typically with a post/image id.
    ///     Default: the URL.
    /// </summary>
    string GetItemKey(FileLink link) => link.Url;

    /// <summary>
    ///     For <see cref="GalleryUpdateStrategy.FullDiff"/>: a value that changes when an existing item's content
    ///     changes (e.g. an edited timestamp), causing it to be downloaded again. Null means "never re-download".
    /// </summary>
    string? GetItemVersion(FileLink link) => null;
}
