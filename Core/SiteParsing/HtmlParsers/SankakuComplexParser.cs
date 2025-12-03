using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class SankakuComplexParser : HtmlParser
{
    public SankakuComplexParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for sankakucomplex.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns></returns>
    protected override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//header[@class='entry-header']/h1[@class='entry-title']/a").InnerText;
        var imagesBase = soup.SelectNodesOrThrow("//a[@class='swipebox']").GetHrefs()[1..];
        var images = imagesBase.Select(image => !image.Contains("http") ? $"https:{image}" : image)
                                .Select(dummy => (StringImageLinkWrapper)dummy).ToList();
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
