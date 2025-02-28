using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class EroThotsParser : HtmlParser
{
    public EroThotsParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for erothots.co and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        string dirName;
        List<StringImageLinkWrapper> images;
        if (CurrentUrl.Contains("/gif/"))
        {
            dirName = soup.SelectSingleNode("//h1[@class='mb-0 title']").InnerText;
            var player = soup.SelectSingleNode("//div[@class='video-player gifs']/video/source");
            images = [player.GetSrc()];
        }
        else if (CurrentUrl.Contains("/video/"))
        {
            dirName = soup.SelectSingleNode("//h1[@class='mb-0 title']").InnerText;
            var player = soup.SelectSingleNode("//video[@class='v-player']/source");
            images = [player.GetSrc()];
        }
        else /*if (CurrentUrl.Contains("/a/"))*/
        {
            dirName = soup.SelectSingleNode("//div[@class='head-title']")
                            .SelectSingleNode(".//span")
                            .InnerText;
            images = soup.SelectSingleNode("//div[@class='album-gallery']")
                            .SelectNodes("./a")
                            .Select(link => link.GetAttributeValue("data-src"))
                            .Select(dummy => (StringImageLinkWrapper)dummy)
                            .ToList();
        }
        
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
