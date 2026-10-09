using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.Booru;
public class E621Parser : BooruParser, IHtmlParser
{
    public static string ParserName => "e621";
    public static string[] SupportedUrls => ["https://e621.net/"];
    public static IReadOnlyList<(string Pattern, UrlMatchKind Kind)> NormalizationPatterns { get; } =
        [("e621.net", UrlMatchKind.Contains)];

    public E621Parser(WebDriver driver,  Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver,  requestHeaders, IHtmlParser.GetFilenameScheme<E621Parser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for e621.net and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        return BooruParse(Booru.E621, cancellationToken: cancellationToken);
    }
    
    public static string NormalizeUrl(string url)
    {
        return BooruUrlNormalization.NormalizeUrl(url, Booru.E621);
    }
}