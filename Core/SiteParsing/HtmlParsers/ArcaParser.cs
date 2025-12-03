using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class ArcaParser : HtmlParser
{
    public ArcaParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for arca.live and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//div[@class='title']").InnerText;
        var mainTag = soup.SelectSingleNodeOrThrow("//div[@class='fr-view article-content']");

        var images = new List<StringImageLinkWrapper>();
        var imageList = mainTag.SelectNodesSafe(".//img").GetSrcs();
        var imgs = imageList
                  .Select(image => image.Split("?")[0] + "?type=orig") // Remove query string and add type=orig
                  .Select(img => !img.Contains(Protocol) ? Protocol + img : img) // Add protocol if missing
                  .Select(dummy => (StringImageLinkWrapper)dummy) // Convert to StringImageLinkWrapper
                  .ToList();
        images.AddRange(imgs);
        
        var videoList = mainTag.SelectNodesSafe(".//video").GetSrcs();
        var videos = videoList
                    .Select(video => !video.Contains(Protocol) ? Protocol + video : video)
                    .Select(dummy => (StringImageLinkWrapper)dummy)
                    .ToList();
        images.AddRange(videos);
   
        
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}