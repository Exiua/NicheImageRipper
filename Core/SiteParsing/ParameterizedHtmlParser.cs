using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing;

public abstract class ParameterizedHtmlParser : HtmlParser
{
    protected ParameterizedHtmlParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    protected override Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        return Parse("", cancellationToken);
    }

    public abstract Task<RipInfo> Parse(string url, CancellationToken cancellationToken = default);
}