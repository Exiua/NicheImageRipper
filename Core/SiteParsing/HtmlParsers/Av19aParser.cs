using System.Text.RegularExpressions;
using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers;

public partial class Av19aParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "av19a";

    public Av19aParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<Av19aParser>(filenameScheme))
    {
    }
    
    /// <summary>
    ///     Parses the html for av19a.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await SolveParseAddCookies();
        var dirName = soup.SelectSingleNodeOrThrow("//header[@class='entry-header']").InnerText;
        var player = soup.SelectSingleNodeOrThrow("//div[@id='player']").SelectSingleNodeOrThrow("./iframe");
        var src = player.GetSrc();
        var match = Av19APlaylistIdRegex().Match(src);
        var urlPath = match.Groups[1].Value;
        var filename = match.Groups[2].Value;
        var playlist = $"https://z124fdsf6dsf.onymyway.top/{urlPath}";
        var linkInfo = new ImageLink(playlist, FilenameScheme, 0, filename: $"{filename}.mp4", linkInfo: LinkInfo.M3U8Ffmpeg)
        {
            Referer = "https://david.cdnbuzz.buzz/"
        };

        return RipInfo.FromUrlList([linkInfo], dirName, FilenameScheme);
    }
    
    [GeneratedRegex(@"vvv=([^&]+).+t=([^&]+)")]
    private static partial Regex Av19APlaylistIdRegex();
}