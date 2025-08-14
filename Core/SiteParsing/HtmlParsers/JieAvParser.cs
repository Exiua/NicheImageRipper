using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.SiteParsing.VideoCapturers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class JieAvParser : HtmlParser
{
    public JieAvParser(WebDriver driver, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, filenameScheme)
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
