using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.Exceptions;
using Core.ExtensionMethods;
using Core.Managers;
using CSWebDriverClient.Models.Responses;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class MissAv123Parser : HtmlParser
{
    public MissAv123Parser(WebDriver driver, ApiClientManager apiClientManager,
                           Dictionary<string, string> requestHeaders,
                           FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, apiClientManager,
        requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for missav123.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await SolveParse();
        var dirNameRaw = soup.SelectSingleNodeOrThrow("//div[@class='mt-4']/h1").InnerText;
        var dirName = dirNameRaw.Contains(',') ? dirNameRaw.Split(',')[0] : dirNameRaw;
        
        var client = new CSWebDriverClient.Client(Config.CSWebDriverUri);
        var urls = await client.GetNetworkUrls(CurrentUrl);
        if (urls is ErrorResponse errorResponse)
        {
            Log.Error("Failed to get network URLs: {Error}", errorResponse.Error);
            throw new RipperException($"Failed to get network URLs: {errorResponse.Error}");
        }
        
        var successResponse = (GetNetworkUrlResponse) urls;
        var playlist = successResponse.Urls.FirstOrDefault(url => url.Contains("playlist.m3u8"));
        if (playlist.IsNullOrEmpty())
        {
            Log.Error("No playlist URL found in network URLs.");
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