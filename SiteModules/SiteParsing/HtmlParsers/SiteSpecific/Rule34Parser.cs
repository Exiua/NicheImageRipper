using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.SiteParsing.HtmlParsers.SiteSpecific;
public class Rule34Parser : BooruParser, IHtmlParser
{
    public static string ParserName => "rule34";
    public static string[] SupportedUrls => ["https://rule34.xxx/"];

    public Rule34Parser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<Rule34Parser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for rule34.xxx and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns></returns>
    protected override Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        return BooruParse(Booru.Rule34);
    }
}