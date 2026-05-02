using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using Core.SiteParsing.VideoCapturers;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class SexBjCamParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "sexbjcam";

    public SexBjCamParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<SexBjCamParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for sexbjcam.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse()
    {
        var soup = await SolveParseAddCookies(regenerateSessionOnFailure: true);
        //Driver.TakeDebugScreenshot();
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='entry-title']").InnerText;
        var iframe = soup.SelectSingleNodeOrThrow("//iframe[@allowfullscreen]");
        var iframeUrl = iframe.GetSrc();
        Logger.Debug("Navigating to iframe URL: {IframeUrl}", iframeUrl);
        var (capturer, b) = await ConfigureNetworkCapture<SexBjCamVideoCapturer>();
        await using var bidi = b;
        CurrentUrl = iframeUrl;
        var referer = iframeUrl.Split("/")[..3].Join("/") + '/';
        StringImageLinkWrapper playlist;
        while (true)
        {
            var links = capturer.GetNewVideoLinks();
            if (links.Count == 0)
            {
                continue;
            }
    
            playlist = new ImageLink(links[0], FilenameScheme, 0)
            {
                Referer = referer,
                LinkInfo = LinkInfo.ObfuscatedM3U8
            };
            break;
        }
        
        return RipInfo.FromUrlList([ playlist ], dirName, FilenameScheme);
    }
}
