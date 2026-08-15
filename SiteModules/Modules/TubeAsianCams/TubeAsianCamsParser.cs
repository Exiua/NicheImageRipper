using System.Text.RegularExpressions;
using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing;
using NicheImageRipper.Core.Utility;
using OpenQA.Selenium;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.TubeAsianCams;
public partial class TubeAsianCamsParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "tubeasiancams";
    public static string[] SupportedUrls => ["https://tubeasiancams.com/"];

    public TubeAsianCamsParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<TubeAsianCamsParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for tubeasiancams.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//h2[@class='entry-title']").InnerText;
        var(capturer, b) = await ConfigureNetworkCapture<TubeAsianCamVideoCapturer>(cancellationToken);
        await using var bidi = b;
        await WaitForElement("//iframe", cancellationToken: cancellationToken);
        var iframe = Driver.FindElement(By.XPath("//iframe"));
        Driver.SwitchTo().Frame(iframe);
        var startButton = Driver.FindElement(By.XPath("//div[@id='a']"));
        startButton.Click();
        var files = new List<StringFileLinkWrapper>();
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
            var fileLink = FileLink.WithFilename(url, $"{filename}.mp4", FilenameScheme, linkInfo: LinkInfo.M3U8Ffmpeg, referer: "https://jilliandescribecompany.com/");
            files.Add(fileLink);
            break;
        }

        return RipInfo.FromUrlList(files, dirName, FilenameScheme);
    }

    [GeneratedRegex("(?<=[?&]mu=)[^&]*")]
    private static partial Regex UrlParameterForEncodedPlaylistUrlRegex();
}