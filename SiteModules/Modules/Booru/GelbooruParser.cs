using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.Booru;
public class GelbooruParser : BooruParser, IHtmlParser, INormalizingHtmlParser
{
    public static string ParserName => "gelbooru";
    public static string[] SupportedUrls => ["https://gelbooru.com/"];
    public static IReadOnlyList<(string Pattern, UrlMatchKind Kind)> NormalizationPatterns { get; } =
        [("gelbooru.", UrlMatchKind.Contains)];

    public GelbooruParser(WebDriver driver,  Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver,  requestHeaders, IHtmlParser.GetFilenameScheme<GelbooruParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for gelbooru.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        return BooruParse(Sdk.Enums.Booru.Gelbooru, cancellationToken: cancellationToken);
    }
    
    public static string NormalizeUrl(string url)
    {
        return BooruUrlNormalization.NormalizeUrl(url, Sdk.Enums.Booru.Gelbooru);
    }
}