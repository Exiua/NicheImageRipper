using Core.DataStructures;
using Core.DataStructures.VideoCapturers;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class JieAvParser : HtmlParser
{
    public JieAvParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for jieav.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//div[@id='works']/h1").InnerText;
        var (capturer, _) = await ConfigureNetworkCapture<JieAvCapturer>();
        Driver.Refresh();
        var images = new List<StringImageLinkWrapper>();
        while (true)
        {
            var videoLinks = capturer.GetNewVideoLinks();
            if (videoLinks.Count == 0)
            {
                await Task.Delay(250);
                continue;
            }
            
            // Only one video of interest
            images.Add(videoLinks[0]);
            break;
        }
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
