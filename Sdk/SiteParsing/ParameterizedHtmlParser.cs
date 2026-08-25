using Sdk.Common;
using Sdk.DataStructures;
using Sdk.Driver;
using Sdk.Enums;

namespace Sdk.SiteParsing;

public abstract class ParameterizedHtmlParser : HtmlParser
{
    /// <summary>
    ///     True when this parser was invoked as a subparser (a specific URL was handed in by another parser),
    ///     false when it's the top-level target of the rip (reached via ParseSite with an empty url). Subclasses
    ///     can check this to skip work that only matters at the top level (e.g. directory-name scraping, since
    ///     the top-level parser's directory name always wins).
    /// </summary>
    protected bool IsSubParserCall { get; private set; }
    
    protected ParameterizedHtmlParser(WebDriver driver, ApiClientManager clientManager,
                                      Dictionary<string, string> requestHeaders,
                                      FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver,
        clientManager, requestHeaders, filenameScheme)
    {
    }

    public sealed override Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        return Parse("", cancellationToken);
    }

    /// <summary>
    ///     Parses the given URL, or the driver's current page if <paramref name="url"/> is empty — used both as
    ///     the entry point when this parser is the top-level target of a rip, and for delegation from another
    ///     parser that needs to hand off a site-specific URL.
    /// </summary>
    public async Task<RipInfo> Parse(string url, CancellationToken cancellationToken = default)
    {
        IsSubParserCall = url != "";
        if (!IsSubParserCall)
        {
            return await ParseCore(cancellationToken);
        }

        GivenUrl = url;
        if (RequiresNavigation)
        {
            CurrentUrl = url;
        }

        if (RequiresLogin)
        {
            await SiteLogin(cancellationToken);
        }

        return await ParseCore(cancellationToken);
    }
    
    /// <summary>
    /// Site-specific scrape logic. CurrentUrl is already set and (if RequiresLogin) login has already run.
    /// </summary>
    protected abstract Task<RipInfo> ParseCore(CancellationToken cancellationToken = default);
}