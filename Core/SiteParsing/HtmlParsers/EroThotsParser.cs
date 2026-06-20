using Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers;

public class EroThotsParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "erothots";

    public EroThotsParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<EroThotsParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for erothots.co and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse(CancellationToken cancellationToken = default)
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
