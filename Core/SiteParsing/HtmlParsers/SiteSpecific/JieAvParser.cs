using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.Managers;
using NicheImageRipper.Core.SiteParsing.VideoCapturers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers.SiteSpecific;

public class JieAvParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "jieav";

    public JieAvParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<JieAvParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for jieav.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//div[@id='works']/h1").InnerText;
        var (capturer, b) = await ConfigureNetworkCapture<JieAvCapturer>();
        await using var bidi = b;
        Driver.Refresh();
        var images = new List<StringFileLinkWrapper>();
        while (true)
        {
            var videoLinks = capturer.GetNewVideoLinks();
            if (videoLinks.Count == 0)
            {
                await Sleep(250);
                continue;
            }
            
            // Only one video of interest
            images.Add(videoLinks[0]);
            break;
        }
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
