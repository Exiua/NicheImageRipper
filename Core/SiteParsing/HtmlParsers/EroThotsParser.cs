using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class EroThotsParser : HtmlParser
{
    public EroThotsParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for erothots.co and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        string dirName;
        List<StringImageLinkWrapper> images;
        if (CurrentUrl.Contains("/gif/"))
        {
            dirName = soup.SelectSingleNodeOrThrow("//h1[@class='mb-0 title']").InnerText;
            var player = soup.SelectSingleNodeOrThrow("//div[@class='video-player gifs']/video/source");
            images = [player.GetSrc()];
        }
        else if (CurrentUrl.Contains("/video/"))
        {
            dirName = soup.SelectSingleNodeOrThrow("//h1[@class='mb-0 title']").InnerText;
            var player = soup.SelectSingleNodeOrThrow("//video[@class='v-player']/source");
            images = [player.GetSrc()];
        }
        else /*if (CurrentUrl.Contains("/a/"))*/
        {
            dirName = soup.SelectSingleNodeOrThrow("//div[@class='head-title']")
                            .SelectSingleNodeOrThrow(".//span")
                            .InnerText;
            images = soup.SelectSingleNodeOrThrow("//div[@class='album-gallery']")
                            .SelectNodesOrThrow("./a")
                            .Select(link => link.GetAttributeValue("data-src"))
                            .Select(dummy => (StringImageLinkWrapper)dummy)
                            .ToList();
        }
        
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
