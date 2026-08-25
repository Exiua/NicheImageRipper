

using Sdk.Common.ExtensionMethods;
using Sdk.DataStructures;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.JieAv;
public class JieAvParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "jieav";
    public static string[] SupportedUrls => ["https://www.jieav.com/"];

    public JieAvParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<JieAvParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for jieav.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify(cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//div[@id='works']/h1").InnerText;
        var(capturer, b) = await ConfigureNetworkCapture<JieAvCapturer>(cancellationToken: cancellationToken);
        await using var bidi = b;
        Driver.Refresh();
        var images = new List<StringFileLinkWrapper>();
        while (true)
        {
            var videoLinks = capturer.GetNewVideoLinks();
            if (videoLinks.Count == 0)
            {
                await Sleep(250, cancellationToken: cancellationToken);
                continue;
            }

            // Only one video of interest
            images.Add(videoLinks[0]);
            break;
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}