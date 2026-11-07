namespace NicheImageRipper.Sdk.SiteParsing;

/// <summary>
///     Swaps the generic browser User-Agent for an app-specific one on requests to certain domains (e.g. sites whose
///     API asks clients to identify themselves). Implementations are discovered by <see cref="UserAgentRuleRegistry"/>.
/// </summary>
public interface IUserAgentRule
{
    /// <summary>Hosts this rule covers; each also matches its subdomains (<c>donmai.us</c> covers <c>cdn.donmai.us</c>).</summary>
    IReadOnlyList<string> Domains { get; }

    /// <summary>User-Agent to send to <see cref="Domains"/> instead of the configured browser one.</summary>
    string UserAgent { get; }
}
