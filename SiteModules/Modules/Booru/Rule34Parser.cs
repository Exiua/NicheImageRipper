

using Sdk.DataStructures;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.Booru;
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
    public override Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        return BooruParse(Core.Enums.Booru.Rule34, cancellationToken: cancellationToken);
    }
}