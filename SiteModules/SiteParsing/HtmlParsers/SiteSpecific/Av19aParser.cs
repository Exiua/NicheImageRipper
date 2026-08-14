using System.Text.RegularExpressions;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.SiteParsing.HtmlParsers.SiteSpecific;
public partial class Av19aParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "av19a";
    public static string[] SupportedUrls => ["https://av19a.com/"];

    public Av19aParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<Av19aParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for av19a.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await SolveParseAddCookies(cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//header[@class='entry-header']").InnerText;
        var player = soup.SelectSingleNodeOrThrow("//div[@id='player']").SelectSingleNodeOrThrow("./iframe");
        var src = player.GetSrc();
        var match = Av19APlaylistIdRegex().Match(src);
        var urlPath = match.Groups[1].Value;
        var filename = match.Groups[2].Value;
        var playlist = $"https://z124fdsf6dsf.onymyway.top/{urlPath}";
        var fileLink = FileLink.WithFilename(playlist, $"{filename}.mp4", FilenameScheme, linkInfo: LinkInfo.M3U8Ffmpeg, referer: "https://david.cdnbuzz.buzz/");
        return RipInfo.FromUrlList([fileLink], dirName, FilenameScheme);
    }

    [GeneratedRegex(@"vvv=([^&]+).+t=([^&]+)")]
    private static partial Regex Av19APlaylistIdRegex();
}