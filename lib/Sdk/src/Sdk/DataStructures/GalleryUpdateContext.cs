namespace NicheImageRipper.Sdk.DataStructures;

/// <summary>
///     What is already known about a gallery from earlier rips, given to an
///     <see cref="SiteParsing.IUpdatableHtmlParser"/> before an update rip so it can stop parsing early.
/// </summary>
/// <param name="PreviousCursor">The <see cref="RipInfo.UpdateCursor"/> saved after the last successful rip, if any.</param>
/// <param name="KnownItemKeys">Keys (from <see cref="SiteParsing.IUpdatableHtmlParser.GetItemKey"/>) of items already ripped.</param>
public sealed record GalleryUpdateContext(string? PreviousCursor, IReadOnlySet<string> KnownItemKeys)
{
    public bool IsKnown(string itemKey) => KnownItemKeys.Contains(itemKey);
}
