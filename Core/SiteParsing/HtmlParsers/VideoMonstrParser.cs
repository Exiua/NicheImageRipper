using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class VideoMonstrParser : HtmlParser
{
    public VideoMonstrParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for site and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var id = CurrentUrl.Split("/")[4];
        var dirName = soup.SelectSingleNodeOrThrow("//h1").InnerText.Split(" – ")[0] + $" ({id})";
        var images = new List<StringImageLinkWrapper>();
        var url = soup.SelectSingleNodeOrThrow("//video").GetSrc();
        images.Add(url);

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}