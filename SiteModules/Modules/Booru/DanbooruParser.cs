using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.Booru;
public class DanbooruParser : BooruParser, IHtmlParser, ISubdomainSignificantHtmlParser, INormalizingHtmlParser
{
    public static string ParserName => "danbooru";
    public static string[] SupportedUrls => ["https://danbooru.donmai.us/"];
    public static int SignificantDomainLabels => 3;
    public static IReadOnlyList<(string Pattern, UrlMatchKind Kind)> NormalizationPatterns { get; } =
        [("danbooru.", UrlMatchKind.Contains)];

    public DanbooruParser(WebDriver driver,  Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver,  requestHeaders, IHtmlParser.GetFilenameScheme<DanbooruParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for danbooru.donmai.us and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        return BooruParse(Booru.Danbooru, cancellationToken: cancellationToken);
    }
    
    public static string NormalizeUrl(string url)
    {
        return BooruUrlNormalization.NormalizeUrl(url, Booru.Danbooru);
    }
}