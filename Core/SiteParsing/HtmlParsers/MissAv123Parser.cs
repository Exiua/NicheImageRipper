using Common.ExtensionMethods;
using CSWebDriverClient.Models.Responses;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Exceptions;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers;

public class MissAv123Parser : HtmlParser, IHtmlParser
{
    public static string ParserName => "missav123";

    public MissAv123Parser(WebDriver driver, ApiClientManager apiClientManager,
                           Dictionary<string, string> requestHeaders,
                           FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, apiClientManager,
        requestHeaders, IHtmlParser.GetFilenameScheme<MissAv123Parser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for missav123.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        if (!NicheImageRipper.AvailableFeatures.HasFlag(ExternalFeatureSupport.CSWebDriver))
        {
            Logger.Error("CSWebDriver URI is not configured. Cannot parse missav123.com without CSWebDriver.");
            throw new FeatureNotAvailableException(ExternalFeatureSupport.CSWebDriver);
        }
        
        var soup = await SolveParse();
        var dirNameRaw = soup.SelectSingleNodeOrThrow("//div[@class='mt-4']/h1").InnerText;
        var dirName = dirNameRaw.Contains(',') ? dirNameRaw.Split(',')[0] : dirNameRaw;
        
        var client = new CSWebDriverClient.Client(Config.CSWebDriverUri);
        var urls = await client.GetNetworkUrls(CurrentUrl);
        if (urls is ErrorResponse errorResponse)
        {
            Logger.Error("Failed to get network URLs: {Error}", errorResponse.Error);
            throw new RipperException($"Failed to get network URLs: {errorResponse.Error}");
        }
        
        var successResponse = (GetNetworkUrlsResponse) urls;
        var playlist = successResponse.Urls.FirstOrDefault(url => url.Contains("playlist.m3u8"));
        if (playlist.IsNullOrEmpty())
        {
            Logger.Error("No playlist URL found in network URLs.");
            throw new RipperException("No playlist URL found in network URLs.");
        }

        var parts = playlist.Split("/");
        var id = parts[3];
        var filename = id + ".mp4";
        var image = new ImageLink(playlist, FilenameScheme, 0)
        {
            LinkInfo = LinkInfo.M3U8YtDlp,
            Filename = filename,
            Referer = CurrentUrl
        };

        return RipInfo.FromUrlList([image], dirName, FilenameScheme);
    }
}