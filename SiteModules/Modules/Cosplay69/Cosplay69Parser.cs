

using Sdk.Common.ExtensionMethods;
using Sdk.DataStructures;
using Sdk.Enums;
using Sdk.SiteParsing;
using WebDriver = Sdk.Driver.WebDriver;

namespace NicheImageRipper.SiteModules.Modules.Cosplay69;
public class Cosplay69Parser : HtmlParser, IHtmlParser
{
    public static string ParserName => "cosplay69";
    public static string[] SupportedUrls => ["https://cosplay69.net/"];

    public Cosplay69Parser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<Cosplay69Parser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses  the HTML for cosplay69.net and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        var soup = await Soupify( /*delay: 5000, */lazyLoadArgs: new LazyLoadArgs { ScrollBy = true, Increment = 1250 }, cancellationToken: cancellationToken);
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='post-title entry-title']").InnerText;
        List<StringFileLinkWrapper> images;
        var video = soup.SelectSingleNode("//iframe");
        if (video is not null)
        {
            var(capturer, _) = await ConfigureNetworkCapture<Cosplay69VideoCapturer>(cancellationToken);
            Driver.Refresh();
            while (true)
            {
                var links = capturer.GetNewVideoLinks();
                if (links.Count == 0)
                {
                    Logger.Debug("No links found, retrying...");
                    await Sleep(1000, cancellationToken: cancellationToken);
                    continue;
                }

                images = links.ToStringFileLinkWrapperList();
                break;
            }
        }
        else
        {
            images = soup.SelectSingleNodeOrThrow("//div[@class='entry-content gridnext-clearfix']").SelectNodesOrThrow(".//img").Select(img => img.GetSrc()).ToStringFileLinkWrapperList();
        }

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}