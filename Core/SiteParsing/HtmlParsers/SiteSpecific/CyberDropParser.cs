using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Exceptions;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.Utility;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;
public class CyberDropParser : ParameterizedHtmlParser, IHtmlParser
{
    public static string ParserName => "cyberdrop";
    public static string[] SupportedUrls => ["https://cyberdrop.me/"];

    private const int ParseDelay = 500;
    public CyberDropParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<CyberDropParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for cyberdrop.me and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> ParseCore(CancellationToken cancellationToken = default)
    {
        var soup = await SolveParseAddCookies(cancellationToken: cancellationToken);
        var titleNode = soup.SelectSingleNode("//h1[@id='title']");
        var dirName = titleNode is not null ? titleNode.InnerText : $"[CyberDrop] {CurrentUrl.Split("/")[^1]}";
        var images = new List<StringFileLinkWrapper>();
        if (CurrentUrl.Contains("/a/"))
        {
            var imageList = soup.SelectNodesOrThrow("//div[@class='image-container column']").Select(image => image.SelectSingleNodeOrThrow(".//a[@class='image']").GetHref()).Select(href => $"https://cyberdrop.me{href}");
            foreach (var image in imageList)
            {
                var link = await GetFileUrl(image, cancellationToken);
                images.Add(link);
            }
        }
        else if (CurrentUrl.Contains("/f/"))
        {
            var link = await GetFileUrl(CurrentUrl, cancellationToken);
            images.Add(link);
        }
        else if (CurrentUrl.Contains("/e/"))
        {
            var video = soup.SelectSingleNode("//video[@id='player']");
            if (video is null)
            {
                await Task.Delay(ParseDelay, cancellationToken);
                soup = await Soupify(delay: ParseDelay, xpath: "//video[@id='player']", cancellationToken: cancellationToken);
                video = soup.SelectSingleNodeOrThrow("//video[@id='player']");
            }

            var link = video.GetVideoSrc();
            images.Add(link);
        }
        else
        {
            Logger.Error("Unknown CyberDrop url type: {CurrentUrl}", CurrentUrl);
            throw new RipperException("Unknown CyberDrop url type");
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }

    private async Task<string> GetFileUrl(string url, CancellationToken cancellationToken = default)
    {
        Logger.Debug("Parsing image: {Image}", url);
        while (true)
        {
            try
            {
                var soup = await Soupify(url, delay: ParseDelay * 2, xpath: "//a[@id='downloadBtn']", xpathTimeout: 120, cancellationToken: cancellationToken);
#if DEBUG
                Driver.TakeDebugScreenshot();
                Logger.Debug("Current url: {CurrentUrl}", Driver.Url);
#endif
                var link = soup.SelectSingleNodeOrThrow("//a[@id='downloadBtn']").GetHref();
                return link;
            }
            catch (AttributeNotFoundException)
            {
                Logger.Debug("Unable to find download button href");
                await Task.Delay(ParseDelay * 2, cancellationToken);
            }
        }
    }
}