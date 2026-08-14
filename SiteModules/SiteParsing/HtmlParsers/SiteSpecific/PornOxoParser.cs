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
public class PornOxoParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "pornoxo";
    public static string[] SupportedUrls => ["https://www.pornoxo.com/"];

    public PornOxoParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<PornOxoParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the HTML for pornoxo.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var id = CurrentUrl.Split("/")[4];
        var(capturer, b) = await ConfigureNetworkCapture<PornOxoCapturer>(cancellationToken);
        await using var bidi = b;
        Driver.Refresh();
        var soup = await Soupify(cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//div[@class='video-top-header']/h1").ChildNodes[0].InnerText.Trim() + $" ({id})";
        var images = new List<StringFileLinkWrapper>();
        await WaitForPlaylist(capturer, links =>
        {
            var link = FileLink.Create(links[0], FilenameScheme, linkInfo: LinkInfo.M3U8Ffmpeg);
            images.Add(link);
        }, cancellationToken);
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}