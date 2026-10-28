namespace NicheImageRipper.Sdk.Enums;

/// <summary>How an <see cref="SiteParsing.IUpdatableHtmlParser"/>'s gallery changes between rips.</summary>
public enum GalleryUpdateStrategy
{
    /// <summary>
    ///     Items are only ever added; past items never change. New items are placed together as one block at
    ///     <see cref="SiteParsing.IUpdatableHtmlParser.NewItemPlacement"/>, and the parser may stop parsing once it
    ///     reaches items it already knows (see <see cref="DataStructures.GalleryUpdateContext"/>).
    /// </summary>
    NewItemsOnly,

    /// <summary>
    ///     Past items can change. The parser must return the whole gallery in order: new items are inserted where
    ///     they appear, existing items are reordered to match, items whose version changed are downloaded again, and
    ///     items no longer listed are kept on disk in their previous place.
    /// </summary>
    FullDiff,
}
