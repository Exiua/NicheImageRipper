using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class HotStunnersParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "hotstunners";

    public HotStunnersParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for hotstunners.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//div[@class='title_content']")
                            .SelectSingleNodeOrThrow(".//h2")
                            .InnerText;
        var images = soup.SelectSingleNodeOrThrow("//div[@class='gallery_janna2']")
                            .SelectNodesOrThrow(".//img")
                            .Select(img => Protocol + img.GetSrc().Remove("tn_"))
                            .ToStringImageLinkWrapperList();
        
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
