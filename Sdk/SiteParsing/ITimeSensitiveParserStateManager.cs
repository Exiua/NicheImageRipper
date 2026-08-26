namespace NicheImageRipper.Sdk.SiteParsing;

public interface ITimeSensitiveParserStateManager
{
    /// <summary>
    ///     Records the link list for a given parser type and rip URL, replacing any previously stored links
    ///     for the same (parserKey, ripUrl) pair.
    /// </summary>
    /// <param name="parserKey">The parser's <c>ParserKey</c>.</param>
    /// <param name="ripUrl">The URL originally given to <c>Rip</c>/<c>ParseSite</c>.</param>
    /// <param name="links">The ordered link list to cache.</param>
    public void StoreLinks(string parserKey, string ripUrl, IReadOnlyList<string> links);

    /// <summary>Looks up the cached link list for a given parser type and rip URL.</summary>
    /// <param name="parserKey">The parser's <c>ParserKey</c>.</param>
    /// <param name="ripUrl">The URL originally given to <c>Rip</c>/<c>ParseSite</c>.</param>
    /// <returns>The cached links in original order, or null if no cached state exists for this (parserKey, ripUrl) pair.</returns>
    public List<string>? GetLinks(string parserKey, string ripUrl);
}