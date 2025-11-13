using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using Core.SiteParsing.VideoCapturers;
using HtmlAgilityPack;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class PussySpaceParser : HtmlParser
{
    public PussySpaceParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for site and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var id = CurrentUrl.Split("-")[1];
        var (capturer, b) = await ConfigureNetworkCapture<PussySpaceCapturer>();
        await using var bidi = b;
        Driver.Refresh();
        var soup = await Soupify();
        var h1 = soup.SelectSingleNodeOrThrow("//h1");
        var dirName = h1.ChildNodes
                        .Where(n => n.NodeType == HtmlNodeType.Text)
                        .Select(n => n.InnerText.Trim())
                        .Where(t => !string.IsNullOrEmpty(t))
                        .Join(" ") + $"({id})";
        var images = new List<StringImageLinkWrapper>();
        WaitForPlaylist(capturer, links =>
        {
            var playlist = new ImageLink(links[0], FilenameScheme, 0)
            {
                LinkInfo = LinkInfo.M3U8YtDlp
            };
            images.Add(playlist);
        });

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}