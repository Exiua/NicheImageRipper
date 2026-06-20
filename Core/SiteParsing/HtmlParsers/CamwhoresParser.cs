using Common.ExtensionMethods;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Managers;
using WebDriver = NicheImageRipper.Core.Driver.WebDriver;

namespace NicheImageRipper.Core.SiteParsing.HtmlParsers;

public class CamwhoresParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "camwhores";

    public CamwhoresParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<CamwhoresParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for site and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override Task<RipInfo> Parse(CancellationToken cancellationToken = default)
    {
        return CamwhoresParse("");
    }

    /// <summary>
    ///     Parses the html for camwhores.tv and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> CamwhoresParse(string url, CancellationToken cancellationToken = default)
    {
        if (url != "")
        {
            CurrentUrl = url;
        }
        
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//div[@class='headline']").SelectSingleNodeOrThrow(".//h1").InnerText;
        var video = soup.SelectSingleNodeOrThrow(".//div[@class='fp-player']").SelectSingleNodeOrThrow(".//video");
        var videoUrl = video.GetSrc();
        var images = new List<StringImageLinkWrapper> { videoUrl };
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
