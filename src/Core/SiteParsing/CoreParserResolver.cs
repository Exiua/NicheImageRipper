using NicheImageRipper.Core.Utility;
using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Driver;
using NicheImageRipper.Sdk.SiteParsing;

namespace NicheImageRipper.Core.SiteParsing;

internal sealed class CoreParserResolver : IParserResolver
{
    public IReadOnlySet<string> DelegatableDomains => HtmlParserFactory.DelegatableDomains;

    public ParameterizedHtmlParser CreateParameterized(string url, HtmlParser caller, WebDriver driver,
                                                       Dictionary<string, string> requestHeaders)
    {
        var calleeHeaders = CopyHeaders(requestHeaders);
        var (siteName, _) = UrlUtility.SiteCheck(url, calleeHeaders);
        var parser = HtmlParserFactory.CreateParameterized(siteName, driver, calleeHeaders, caller.FilenameScheme);
        HtmlParserOrchestrator.ApplySubParserOverrides(parser, caller);
        return parser;
    }

    public Task<RipInfo> ParseSite(string url, HtmlParser caller, WebDriver driver,
                                   Dictionary<string, string> requestHeaders,
                                   CancellationToken cancellationToken = default)
    {
        var orchestrator = new HtmlParserOrchestrator(driver, CopyHeaders(requestHeaders), caller.FilenameScheme,
            caller);
        return orchestrator.ParseSite(url, cancellationToken);
    }

    /// <summary>
    ///     Gives a delegated parser its own headers, seeded from the caller's, so the referer its site check sets (and
    ///     any cookie/auth it adds) doesn't leak back into the caller's remaining requests or partial save.
    /// </summary>
    private static Dictionary<string, string> CopyHeaders(Dictionary<string, string> requestHeaders) =>
        new(requestHeaders, requestHeaders.Comparer);
}