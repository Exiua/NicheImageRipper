using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using Core.SiteParsing.VideoCapturers;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class Cosplay69Parser : HtmlParser, IHtmlParser
{
    public static string ParserName => "cosplay69";

    public Cosplay69Parser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<Cosplay69Parser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for cosplay69.net and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse()
    {
        var soup = await Soupify(/*delay: 5000, */lazyLoadArgs: new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250
        });
    
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='post-title entry-title']").InnerText;
        List<StringImageLinkWrapper> images;
        var video = soup.SelectSingleNode("//iframe");
        if (video is not null)
        {
            var (capturer, _) = await ConfigureNetworkCapture<Cosplay69VideoCapturer>();
            Driver.Refresh();
            while (true)
            {
                var links = capturer.GetNewVideoLinks();
                if (links.Count == 0)
                {
                    Log.Debug("No links found, retrying...");
                    await Sleep(1000);
                    continue;
                }
                
                images = links.ToStringImageLinkWrapperList();
                break;
            }
        }
        else
        {
            images = soup.SelectSingleNodeOrThrow("//div[@class='entry-content gridnext-clearfix']")
                            .SelectNodesOrThrow(".//img")
                            .Select(img => img.GetSrc())
                            .ToStringImageLinkWrapperList();
        }
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
