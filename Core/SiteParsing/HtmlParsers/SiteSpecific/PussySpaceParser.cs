using HtmlAgilityPack;
using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing.VideoCapturers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;

public class PussySpaceParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "pussyspace";

    public PussySpaceParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<PussySpaceParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the HTML for site and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var id = CurrentUrl.Split("-")[1];
        var (capturer, b) = await ConfigureNetworkCapture<PussySpaceCapturer>(cancellationToken);
        await using var bidi = b;
        Driver.Refresh();
        var soup = await Soupify(cancellationToken: cancellationToken);
        var h1 = soup.SelectSingleNodeOrThrow("//h1");
        var dirName = h1.ChildNodes
                        .Where(n => n.NodeType == HtmlNodeType.Text)
                        .Select(n => n.InnerText.Trim())
                        .Where(t => !string.IsNullOrEmpty(t))
                        .Join(" ") + $"({id})";
        var images = new List<StringFileLinkWrapper>();
        await WaitForPlaylist(capturer, links =>
        {
            var playlist = FileLink.Create(links[0], FilenameScheme, linkInfo: LinkInfo.M3U8YtDlp);
            images.Add(playlist);
        }, cancellationToken);

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}