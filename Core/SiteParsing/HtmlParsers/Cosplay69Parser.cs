using Core.DataStructures;
using Core.DataStructures.VideoCapturers;
using Core.Enums;
using Core.ExtensionMethods;
using Serilog;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class Cosplay69Parser : HtmlParser
{
    public Cosplay69Parser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for cosplay69.net and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify(/*delay: 5000, */lazyLoadArgs: new LazyLoadArgs
        {
            ScrollBy = true,
            Increment = 1250
        });
    
        var dirName = soup.SelectSingleNode("//h1[@class='post-title entry-title']").InnerText;
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
                    await Task.Delay(1000);
                    continue;
                }
                
                images = links.ToStringImageLinkWrapperList();
                break;
            }
        }
        else
        {
            images = soup.SelectSingleNode("//div[@class='entry-content gridnext-clearfix']")
                            .SelectNodes(".//img")
                            .Select(img => img.GetSrc())
                            .ToStringImageLinkWrapperList();
        }
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
