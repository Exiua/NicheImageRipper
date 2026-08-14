using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using NicheImageRipper.Core.Exceptions;
using NicheImageRipper.Core.SiteParsing.HtmlParsers;
using OpenQA.Selenium;
using HtmlAgilityPack;
using NicheImageRipper.SiteModules.SiteParsing.VideoCapturers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.SiteParsing.HtmlParsers.SiteSpecific;

public class BestCamParser : HtmlParser
{
    public BestCamParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders,
                         FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager,
        requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses  the HTML for bestcam.tv and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var (capturer, b) = await ConfigureNetworkCapture<BestCamVideoCapturer>(cancellationToken);
        await using var bidi = b;
        var soup = await SolveParse(cancellationToken: cancellationToken);
        string dirName;
        var images = new List<StringFileLinkWrapper>();
        if (CurrentUrl.Contains("/model/"))
        {
            dirName = soup.SelectSingleNodeOrThrow("//div[@class='actor-name']/h1").InnerText;
        }
        else
        {
            dirName = soup.SelectSingleNodeOrThrow("//h1[@class='movie-detail-name']").InnerText;
            var playButton = Driver.FindElement(By.XPath("//div[@class='play-icon']"));
            playButton.Click();
            await WaitForPlaylist(capturer, links =>
            {
                var url = links[0];
                var filename = url.Split("/")[4].Split("?")[0].Remove(".m3u8") + ".mp4";
                var link = FileLink.WithFilename(url, filename, FilenameScheme, linkInfo: LinkInfo.M3U8Ffmpeg,
                    referer: CurrentUrl);
                images.Add(link);
            }, cancellationToken);
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}