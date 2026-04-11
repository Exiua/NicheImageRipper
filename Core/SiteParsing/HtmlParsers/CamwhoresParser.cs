using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

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
    protected override Task<RipInfo> Parse()
    {
        return CamwhoresParse("");
    }

    /// <summary>
    ///     Parses the html for camwhores.tv and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    private async Task<RipInfo> CamwhoresParse(string url)
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
