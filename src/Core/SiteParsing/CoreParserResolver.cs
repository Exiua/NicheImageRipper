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
        var (siteName, _) = UrlUtility.SiteCheck(url, requestHeaders);
        var parser = HtmlParserFactory.CreateParameterized(siteName, driver, requestHeaders, caller.FilenameScheme);
        HtmlParserOrchestrator.ApplySubParserOverrides(parser, caller);
        return parser;
    }

    public Task<RipInfo> ParseSite(string url, HtmlParser caller, WebDriver driver,
                                   Dictionary<string, string> requestHeaders,
                                   CancellationToken cancellationToken = default)
    {
        var orchestrator = new HtmlParserOrchestrator(driver, requestHeaders, caller.FilenameScheme, caller);
        return orchestrator.ParseSite(url, cancellationToken);
    }
}