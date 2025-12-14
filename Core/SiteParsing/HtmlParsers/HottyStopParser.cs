using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class HottyStopParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "hottystop";

    public HottyStopParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for hottystop.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var boxLargeContent = soup.SelectSingleNodeOrThrow("//div[@class='content-center content-center-2']");
        var titleNode = boxLargeContent.SelectSingleNode(".//h1") ?? boxLargeContent.SelectSingleNodeOrThrow(".//u");
        var dirName = titleNode.InnerText;
        var images = soup.SelectSingleNodeOrThrow("//ul[@class='gallery']")
                            .SelectNodesOrThrow(".//a")
                            .Select(a => a.GetHref())
                            .ToStringImageLinkWrapperList();
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
