using System.Text.RegularExpressions;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public partial class Av19aParser : HtmlParser
{
    public Av19aParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }
    
    /// <summary>
    ///     Parses the html for av19a.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await SolveParseAddCookies();
        var dirName = soup.SelectSingleNode("//header[@class='entry-header']").InnerText;
        var player = soup.SelectSingleNode("//div[@id='player']").SelectSingleNode("./iframe");
        var src = player.GetSrc();
        var match = Av19APlaylistIdRegex().Match(src);
        var urlPath = match.Groups[1].Value;
        var filename = match.Groups[2].Value;
        var playlist = $"https://z124fdsf6dsf.onymyway.top/{urlPath}";
        var linkInfo = new ImageLink(playlist, FilenameScheme, 0, filename: $"{filename}.mp4", linkInfo: LinkInfo.M3U8Ffmpeg)
        {
            Referer = "https://david.cdnbuzz.buzz/"
        };

        return new RipInfo([linkInfo], dirName, FilenameScheme);
    }
    
    [GeneratedRegex(@"vvv=([^&]+).+t=([^&]+)")]
    private static partial Regex Av19APlaylistIdRegex();
}