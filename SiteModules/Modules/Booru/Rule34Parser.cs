using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.Booru;
public class Rule34Parser : BooruParser, IHtmlParser
{
    public static string ParserName => "rule34";
    public static string[] SupportedUrls => ["https://rule34.xxx/"];

    public Rule34Parser(WebDriver driver,  Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver,  requestHeaders, IHtmlParser.GetFilenameScheme<Rule34Parser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for rule34.xxx and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns></returns>
    public override Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        return BooruParse(Sdk.Enums.Booru.Rule34, cancellationToken: cancellationToken);
    }
}