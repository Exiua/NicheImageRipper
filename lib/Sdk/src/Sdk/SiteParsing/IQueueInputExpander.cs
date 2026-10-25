namespace NicheImageRipper.Sdk.SiteParsing;

/// <summary>
///     Turns shorthand typed in place of URLs (e.g. <c>booru tag1 tag2</c>) into the URLs to queue. Implementations
///     live in site modules and are discovered by Core, so every client (GUI, Tui, Service) gets the same shorthands.
/// </summary>
public interface IQueueInputExpander
{
    /// <returns>The URLs to queue, or null if this expander doesn't handle <paramref name="input"/>.</returns>
    IReadOnlyList<string>? TryExpand(string input);
}
