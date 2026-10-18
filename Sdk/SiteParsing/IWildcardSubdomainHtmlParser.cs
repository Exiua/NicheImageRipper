namespace NicheImageRipper.Sdk.SiteParsing;

/// <summary>
///     Marks a parser that also handles every subdomain of its <see cref="IHtmlParser.SupportedUrls"/> hosts, for
///     sites that put content under per-user subdomains (e.g. <c>someone.newgrounds.com</c>).
/// </summary>
public interface IWildcardSubdomainHtmlParser : IHtmlParser;
