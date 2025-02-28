using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Serilog;
using WebDriver = Core.History.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class CatBoxParser : HtmlParser
{
    public CatBoxParser(WebDriver driver, Dictionary<string, string> requestHeaders, string siteName = "", FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, requestHeaders, siteName, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for catbox.moe and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    public override async Task<RipInfo> Parse()
    {
        Log.Warning("Catbox.moe support is experimental and may not work as expected");
        var soup = await Soupify();
        var dirName = soup.SelectSingleNode("//div[@class='title']/h1").InnerText;
        var images = soup.SelectSingleNode("//div[@class='imagecontainer']")
                            .SelectNodes("./video")
                            .Select(vid => vid.GetSrc())
                            .ToStringImageLinkWrapperList();
    
        return new RipInfo(images, dirName, FilenameScheme);
    }
}
