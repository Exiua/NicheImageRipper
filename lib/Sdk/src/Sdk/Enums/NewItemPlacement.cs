namespace NicheImageRipper.Sdk.Enums;

/// <summary>
///     Where new items go relative to already-ripped ones for <see cref="GalleryUpdateStrategy.NewItemsOnly"/>
///     parsers (e.g. <see cref="Start"/> for feeds listed newest-first).
/// </summary>
public enum NewItemPlacement
{
    End,
    Start,
}
