using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Driver;
using NicheImageRipper.Sdk.Enums;

namespace NicheImageRipper.Sdk.SiteParsing;

public interface IParserResolver
{
    IReadOnlySet<string> DelegatableDomains { get; }

    /// <summary>Resolves and constructs the parser for an embedded link (e.g. a Mega/GDrive/GoFile
    /// link found inside another site's page), for delegated (non-top-level) parsing.</summary>
    ParameterizedHtmlParser CreateParameterized(string url, WebDriver driver,
                                                Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme);

    /// <summary>Runs another site's full top-level parse pipeline (site detection, partial-save
    /// caching, retry) against <paramref name="url"/> — for parsers that alias/redirect wholesale
    /// to a different registered site.</summary>
    Task<RipInfo> ParseSite(string url, WebDriver driver, Dictionary<string, string> requestHeaders,
                            FilenameScheme filenameScheme, CancellationToken cancellationToken = default);
}