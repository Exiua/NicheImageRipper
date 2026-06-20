using System.Text.RegularExpressions;
using Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing.VideoCapturers;
using NicheImageRipper.Core.Utility;
using OpenQA.Selenium;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers;

public partial class TubeAsianCamsParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "tubeasiancams";

    public TubeAsianCamsParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<TubeAsianCamsParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for tubeasiancams.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//h2[@class='entry-title']").InnerText;
        var (capturer, b) = await ConfigureNetworkCapture<TubeAsianCamVideoCapturer>();
        await using var bidi = b;
        await WaitForElement("//iframe");
        var iframe = Driver.FindElement(By.XPath("//iframe"));
        Driver.SwitchTo().Frame(iframe);
        var startButton = Driver.FindElement(By.XPath("//div[@id='a']"));
        startButton.Click();
        var images = new List<StringImageLinkWrapper>();
        while (true)
        {
            var links = capturer.GetNewVideoLinks();
            if (links.Count == 0)
            {
                continue;
            }

            string url;
            var match = UrlParameterForEncodedPlaylistUrlRegex().Match(links[0]);
            if (match.Success)
            {
                url = match.Value;
                url = Uri.UnescapeDataString(url);
            }
            else
            {
                url = links[0];
            }
            
            var filename = UrlUtility.GetUrlParameterValue(url, "t");
            var imageLink = new ImageLink(url, FilenameScheme, 0, filename: filename + ".mp4")
            {
                Referer = "https://jilliandescribecompany.com/",
                LinkInfo = LinkInfo.M3U8YtDlp
            };
            images.Add(imageLink);
            break;
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }

    [GeneratedRegex("(?<=[?&]mu=)[^&]*")]
    private static partial Regex UrlParameterForEncodedPlaylistUrlRegex();
}