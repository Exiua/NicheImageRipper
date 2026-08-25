namespace Sdk.SiteParsing;

public interface ISiteLinkRule
{
    string RuleName { get; }

    /// <summary>Whether this rule owns the given (trimmed, decoded) raw URL.</summary>
    bool Matches(string url);

    /// <summary>
    ///     Resolve the raw URL into its cleaned form, referer, LinkInfo, and (optionally) filename.
    ///     Return Filename = null to fall through to the generic path-based filename extractor.
    /// </summary>
    SiteLinkInfo Resolve(string url);
}
