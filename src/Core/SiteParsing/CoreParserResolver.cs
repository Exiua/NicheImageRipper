using NicheImageRipper.Core.Utility;
using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Driver;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;

namespace NicheImageRipper.Core.SiteParsing;

internal sealed class CoreParserResolver : IParserResolver
{
    public IReadOnlySet<string> DelegatableDomains => HtmlParserFactory.DelegatableDomains;

    public ParameterizedHtmlParser CreateParameterized(string url, WebDriver driver,
                                                       Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme)
    {
        var (siteName, _) = UrlUtility.SiteCheck(url, requestHeaders);
        return HtmlParserFactory.CreateParameterized(siteName, driver, requestHeaders, filenameScheme);
    }

    public Task<RipInfo> ParseSite(string url, WebDriver driver, Dictionary<string, string> requestHeaders,
                                   FilenameScheme filenameScheme, CancellationToken cancellationToken = default)
    {
        var orchestrator = new HtmlParserOrchestrator(driver, requestHeaders, filenameScheme);
        return orchestrator.ParseSite(url, cancellationToken);
    }
}