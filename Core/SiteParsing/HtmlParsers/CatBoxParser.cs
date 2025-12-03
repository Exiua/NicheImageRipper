using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using Serilog;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class CatBoxParser : HtmlParser
{
    public CatBoxParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for catbox.moe and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse()
    {
        Log.Warning("Catbox.moe support is experimental and may not work as expected");
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//div[@class='title']/h1").InnerText;
        var images = soup.SelectSingleNodeOrThrow("//div[@class='imagecontainer']")
                            .SelectNodesOrThrow("./video")
                            .Select(vid => vid.GetSrc())
                            .ToStringImageLinkWrapperList();
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
