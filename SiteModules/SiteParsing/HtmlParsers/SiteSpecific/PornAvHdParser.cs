using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.SiteParsing.HtmlParsers.SiteSpecific;
public class PornAvHdParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "pornavhd";
    public static string[] SupportedUrls => ["https://pornavhd.com/"];

    public PornAvHdParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<PornAvHdParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the HTML for pornavhd.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await SolveParseAddCookies(cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@itemprop='name']").InnerText;
        var iframe = soup.SelectSingleNodeOrThrow("//div[@class='responsive-player']/iframe");
        var iframeUrl = iframe.GetSrc();
        var(capturer, _) = await ConfigureNetworkCapture<SexBjCamVideoCapturer>(cancellationToken);
        CurrentUrl = iframeUrl;
        var referer = iframeUrl.Split("/")[..3].Join("/") + '/';
        StringFileLinkWrapper playlist;
        while (true)
        {
            var links = capturer.GetNewVideoLinks();
            if (links.Count == 0)
            {
                continue;
            }

            playlist = FileLink.Create(links[0], FilenameScheme, referer: referer);
            break;
        }

        return RipInfo.FromUrlList([playlist], dirName, FilenameScheme);
    }
}