using NicheImageRipper.Sdk.Enums;

namespace NicheImageRipper.Sdk.SiteParsing;

public interface INormalizingHtmlParser : IHtmlParser
{
    /// <summary>
    /// Host-matching patterns that route a URL to <see cref="NormalizeUrl"/>, each matched against
    /// <see cref="Uri.Host"/> using its <see cref="UrlMatchKind"/>. Decoupled from
    /// <see cref="IHtmlParser.SupportedUrls"/> since normalization sometimes needs to catch host
    /// variants (e.g. "exhentai.org" routing to the e-hentai parser) that aren't themselves a
    /// registered supported URL.
    /// </summary>
    static abstract IReadOnlyList<(string Pattern, UrlMatchKind Kind)> NormalizationPatterns { get; }

    /// <summary>
    /// Normalizes a URL for this site before queueing/caching — e.g. stripping tracking params,
    /// canonicalizing query args, or rewriting to a canonical host. Called once per URL, before
    /// site detection or partial-save lookup. Must be pure and side-effect-free.
    /// </summary>
    static abstract string NormalizeUrl(string url);
}