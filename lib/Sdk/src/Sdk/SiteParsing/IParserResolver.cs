using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Driver;

namespace NicheImageRipper.Sdk.SiteParsing;

public interface IParserResolver
{
    IReadOnlySet<string> DelegatableDomains { get; }

    /// <summary>Resolves and constructs the parser for an embedded link (e.g. a Mega/GDrive/GoFile
    /// link found inside another site's page), for delegated (non-top-level) parsing. Its overrides are
    /// resolved against <paramref name="caller"/>'s per <see cref="Configuration.GeneralConfig.SubParserOverrides"/>.</summary>
    ParameterizedHtmlParser CreateParameterized(string url, HtmlParser caller, WebDriver driver,
                                                Dictionary<string, string> requestHeaders);

    /// <summary>Runs another site's full top-level parse pipeline (site detection, partial-save
    /// caching, retry) against <paramref name="url"/> — for parsers that alias/redirect wholesale
    /// to a different registered site. Overrides are resolved against <paramref name="caller"/>'s as in
    /// <see cref="CreateParameterized"/>.</summary>
    Task<RipInfo> ParseSite(string url, HtmlParser caller, WebDriver driver, Dictionary<string, string> requestHeaders,
                            CancellationToken cancellationToken = default);
}