using System.Text.RegularExpressions;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.SiteParsing.HtmlParsers.SiteSpecific;
public partial class HentaiCosplaysParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "hentai-cosplays";
    public static string[] SupportedUrls => ["https://hentai-cosplays.com/"];

    public HentaiCosplaysParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<HentaiCosplaysParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for hentai-cosplays.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        if (CurrentUrl.Contains("/video/"))
        {
            CurrentUrl = CurrentUrl.Replace("hentai-cosplays.com", "porn-video-xxx.com");
            var parser = new PornVideoXXXParser(WebDriver, ApiClientManager, RequestHeaders, FilenameScheme);
            return await parser.ParseSite(CurrentUrl);
        }

        var soup = await Soupify(lazyLoadArgs: new LazyLoadArgs { ScrollBy = true });
        var dirName = soup.SelectSingleNodeOrThrow("//div[@id='main_contents']//h2").InnerText;
        var images = new List<StringFileLinkWrapper>();
        while (true)
        {
            var imageList = soup.SelectSingleNodeOrThrow("//div[@id='display_image_detail']").SelectNodesSafe(".//img").Select(img => img.GetSrc()).Select(img => HentaiCosplayRegex().Replace(img, "")).Select(dummy => (StringFileLinkWrapper)dummy).ToList();
            images.AddRange(imageList);
            var nextPage = soup.SelectSingleNodeOrThrow("//div[@id='paginator']").SelectNodesOrThrow(".//span")[^2].SelectSingleNode(".//a");
            if (nextPage is null)
            {
                break;
            }

            soup = await Soupify($"https://hentai-cosplays.com{nextPage.GetHref()}", lazyLoadArgs: new LazyLoadArgs { ScrollBy = true });
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }

    [GeneratedRegex(@"(/p=\d+)")]
    private static partial Regex HentaiCosplayRegex();
}