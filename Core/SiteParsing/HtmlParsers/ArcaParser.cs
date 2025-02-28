using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class ArcaParser : HtmlParser
{
    public ArcaParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for arca.live and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//div[@class='title']").InnerText;
        var mainTag = soup.SelectSingleNode("//div[@class='fr-view article-content']");

        var images = new List<StringImageLinkWrapper>();
        var imgNodes = mainTag.SelectNodes(".//img");
        if(imgNodes is not null)
        {
            var imageList = mainTag.SelectNodes(".//img").GetSrcs();
            var imgs = imageList
                      .Select(image => image.Split("?")[0] + "?type=orig") // Remove query string and add type=orig
                      .Select(img => !img.Contains(Protocol) ? Protocol + img : img) // Add protocol if missing
                      .Select(dummy => (StringImageLinkWrapper)dummy) // Convert to StringImageLinkWrapper
                      .ToList();
            images.AddRange(imgs);
        }
        
        var videoNodes = mainTag.SelectNodes(".//video");
        if(videoNodes is not null)
        {
            var videoList = mainTag.SelectNodes(".//video").GetSrcs();
            var videos = videoList
                        .Select(video => !video.Contains(Protocol) ? Protocol + video : video)
                        .Select(dummy => (StringImageLinkWrapper)dummy)
                        .ToList();
            images.AddRange(videos);
        }
        
        return new RipInfo(images, dirName, FilenameScheme);
    }
}