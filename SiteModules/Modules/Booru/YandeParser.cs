using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.Booru;

public class YandeParser : BooruParser, IHtmlParser, INormalizingHtmlParser
{
    public static string ParserName => "yande";
    public static string[] SupportedUrls => ["https://yande.re/"];
    public static IReadOnlyList<(string Pattern, UrlMatchKind Kind)> NormalizationPatterns { get; } =
        [("yande.re", UrlMatchKind.Contains)];

    public YandeParser(WebDriver driver, Dictionary<string, string> requestHeaders,
                       FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver,
        requestHeaders, IHtmlParser.GetFilenameScheme<YandeParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for yande.re and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        return BooruParse(Sdk.Enums.Booru.Yandere, cancellationToken: cancellationToken);
    }
    
    public static string NormalizeUrl(string url)
    {
        return BooruUrlNormalization.NormalizeUrl(url, Sdk.Enums.Booru.Yandere);
    }
}