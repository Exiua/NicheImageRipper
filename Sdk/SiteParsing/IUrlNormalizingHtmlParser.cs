namespace NicheImageRipper.Sdk.SiteParsing;

public interface IUrlNormalizingHtmlParser : IHtmlParser
{
    /// <summary>Substring replacements to apply to a URL before site detection/navigation (e.g. "members." -> "www.").</summary>
    static abstract (string From, string To)[] UrlReplacements { get; }
}