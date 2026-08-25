

using Sdk.DataStructures;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.VideoMonstr;
public class VideoMonstrParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "videomonstr";
    public static string[] SupportedUrls => ["https://videomonstr.com/"];

    public VideoMonstrParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<VideoMonstrParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for site and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(cancellationToken: cancellationToken);
        var id = CurrentUrl.Split("/")[4];
        var dirName = soup.SelectSingleNodeOrThrow("//h1").InnerText.Split(" – ")[0] + $" ({id})";
        var images = new List<StringFileLinkWrapper>();
        var url = soup.SelectSingleNodeOrThrow("//video").GetSrc();
        images.Add(url);
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}