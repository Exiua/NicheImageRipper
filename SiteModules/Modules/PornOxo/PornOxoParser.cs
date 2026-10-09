using NicheImageRipper.Sdk.Common.ExtensionMethods;
using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.SiteParsing;
using WebDriver = NicheImageRipper.Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.PornOxo;
public class PornOxoParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "pornoxo";
    public static string[] SupportedUrls => ["https://www.pornoxo.com/"];

    public PornOxoParser(WebDriver driver,  Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver,  requestHeaders, IHtmlParser.GetFilenameScheme<PornOxoParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the HTML for pornoxo.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
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