using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.SiteParsing.HtmlParsers.SiteSpecific;
public class LeakedBbParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "leakedbb";
    public static string[] SupportedUrls => ["https://leakedbb.com/"];

    public LeakedBbParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<LeakedBbParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the HTML for leakedbb.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//div[@class='flow-text left']").SelectSingleNodeOrThrow(".//h1").InnerText;
        var imageLinks = soup.SelectSingleNodeOrThrow("//div[@class='post_body scaleimages']").SelectNodesOrThrow("./img").Select(img => img.GetSrc()).ToList();
        var images = new List<StringFileLinkWrapper>();
        foreach (var link in imageLinks)
        {
            if (!link.Contains("postimg.cc"))
            {
                images.Add(link);
                continue;
            }

            soup = await Soupify(link);
            var img = soup.SelectSingleNodeOrThrow("//a[@id='download']").GetHref().Split("?")[0];
            images.Add(img);
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}