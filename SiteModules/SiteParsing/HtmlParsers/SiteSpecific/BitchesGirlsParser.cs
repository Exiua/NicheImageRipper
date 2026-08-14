using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.SiteParsing.HtmlParsers.SiteSpecific;
public class BitchesGirlsParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "bitchesgirls";
    public static string[] SupportedUrls => ["https://bitchesgirls.com/"];

    public BitchesGirlsParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<BitchesGirlsParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for bitchesgirls.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='album-name']").InnerText;
        var images = new List<StringFileLinkWrapper>();
        var baseUrl = CurrentUrl;
        if (baseUrl[^1] != '/')
        {
            baseUrl += "/";
        }

        var page = 1;
        while (true)
        {
            if (page != 1)
            {
                soup = await Soupify($"{baseUrl}{page}");
            }

            var posts = soup.SelectSingleNodeOrThrow("//div[@class='albumgrid']").SelectNodesOrThrow("./a[@class='post-container']").Select(post => post.GetHref()).ToStringImageLinkWrapperList();
            images.AddRange(posts);
            var loadBtn = soup.SelectSingleNode("//a[@id='loadMore']");
            if (loadBtn is not null)
            {
                page += 1;
            }
            else
            {
                break;
            }
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}