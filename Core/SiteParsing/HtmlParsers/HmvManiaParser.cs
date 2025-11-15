using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class HmvManiaParser : HtmlParser
{
    public HmvManiaParser(WebDriver driver, ApiClientManager apiClientManager,
                          Dictionary<string, string> requestHeaders,
                          FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, apiClientManager,
        requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for hmvmania.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='video-entry-title']").InnerText;
        var images = new List<StringImageLinkWrapper>();
        var videoUrl = soup.SelectSingleNodeOrThrow("//li[i[@class='fas fa-download']]/a").GetHref();
        images.Add(videoUrl);

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}