using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using NicheImageRipper.Core.Exceptions;
using NicheImageRipper.Core.SiteParsing.HtmlParsers;
using OpenQA.Selenium;
using HtmlAgilityPack;
using NicheImageRipper.SiteModules.SiteParsing.VideoCapturers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.SiteParsing.HtmlParsers.SiteSpecific;
public class XVideosParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "xvideos";
    public static string[] SupportedUrls => ["https://www.xvideos.com/"];

    public XVideosParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<XVideosParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for xvideos.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var currentUrl = CurrentUrl;
        var id = currentUrl.Split("/")[3].Split(".")[1];
        var(capturer, b) = await ConfigureNetworkCapture<XVideosCapturer>(cancellationToken);
        await using var bidi = b;
        Driver.Refresh();
        var soup = await Soupify(cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//h2[@class='page-title']").ChildNodes[0].InnerText + $" ({id})";
        var files = new List<StringFileLinkWrapper>();
        await WaitForPlaylist(capturer, links =>
        {
            var url = links[0];
            var filename = url.Split("/")[^2] + ".mp4";
            var link = FileLink.WithFilename(url, filename, FilenameScheme, linkInfo: LinkInfo.M3U8Ffmpeg, referer: CurrentUrl);
            files.Add(link);
        }, cancellationToken);
        return RipInfo.FromUrlList(files, dirName, FilenameScheme);
    }
}