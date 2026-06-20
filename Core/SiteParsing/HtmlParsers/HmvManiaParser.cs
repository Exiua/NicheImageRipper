using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers;

public class HmvManiaParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "hmvmania";

    public HmvManiaParser(WebDriver driver, ApiClientManager apiClientManager,
                          Dictionary<string, string> requestHeaders,
                          FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, apiClientManager,
        requestHeaders, IHtmlParser.GetFilenameScheme<HmvManiaParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for hmvmania.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='video-entry-title']").InnerText;
        var images = new List<StringImageLinkWrapper>();
        var videoUrl = soup.SelectSingleNodeOrThrow("//li[i[@class='fas fa-download']]/a").GetHref();
        images.Add(videoUrl);

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}