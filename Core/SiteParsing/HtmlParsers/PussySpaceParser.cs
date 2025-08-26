using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.SiteParsing.VideoCapturers;
using HtmlAgilityPack;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class PussySpaceParser : HtmlParser
{
    public PussySpaceParser(WebDriver driver, Dictionary<string, string> requestHeaders,
                            FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders,
        filenameScheme)
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
        while (true)
        {
            var links = capturer.GetNewVideoLinks();
            if (links.Count == 0)
            {
                continue;
            }
    
            var playlist = new ImageLink(links[0], FilenameScheme, 0)
            {
                LinkInfo = LinkInfo.M3U8YtDlp
            };
            images.Add(playlist);
            break;
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}