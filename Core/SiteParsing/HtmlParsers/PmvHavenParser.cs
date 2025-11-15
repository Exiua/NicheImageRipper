using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class PmvHavenParser : HtmlParser
{
    public PmvHavenParser(WebDriver driver, ApiClientManager apiClientManager,
                          Dictionary<string, string> requestHeaders,
                          FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, apiClientManager,
        requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for pmvhaven.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        await WaitForElement("//video[@id='VideoPlayer']/source", 10);
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//h1[@class='pl-2']").InnerText;
        var images = new List<StringImageLinkWrapper>();
        var video = soup.SelectSingleNodeOrThrow("//video[@id='VideoPlayer']/source");
        var url = video.GetSrc();
        images.Add(url);

        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}