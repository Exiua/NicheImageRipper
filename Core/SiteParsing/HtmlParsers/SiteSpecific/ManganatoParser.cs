using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;

public class ManganatoParser : HtmlParser, IHtmlParser, IMultiSiteHtmlParser
{
    public static string ParserName => "manganato";

    public static string[] AdditionalParserNames { get; } = ["chapmanganato"];

    public static string[] SupportedUrls { get; } =
    [
        "https://readmanganato.com/",
        "https://manganato.com/"
    ];

    public ManganatoParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<ManganatoParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the HTML for manganato.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//div[@class='story-info-right']")
                            .SelectSingleNodeOrThrow(".//h1")
                            .InnerText;
        var nextChapter = soup.SelectSingleNodeOrThrow("//ul[@class='row-content-chapter']")
                            .SelectNodesOrThrow("./li")[^1]
                            .SelectSingleNode(".//a");
        var images = new List<StringFileLinkWrapper>();
        var counter = 1;
        while (nextChapter is not null)
        {
            Logger.Information($"Parsing Chapter {counter}");
            counter += 1;
            soup = await Soupify(nextChapter.GetHref());
            var chapterImages = soup.SelectSingleNodeOrThrow("//div[@class='container-chapter-reader']")
                                    .SelectNodesOrThrow(".//img");
            images.AddRange(chapterImages.Select(img => (StringFileLinkWrapper)img.GetSrc()));
            nextChapter = soup.SelectSingleNode("//a[@class='navi-change-chapter-btn-next a-h']");
        }
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
