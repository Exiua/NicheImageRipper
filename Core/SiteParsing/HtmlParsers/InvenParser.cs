using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class InvenParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "inven";

    public InvenParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for inven.co.kr and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//div[@class='subject ']//span[@class='middle']")
                            .InnerText;
        var images = soup.SelectSingleNodeOrThrow("//div[@id='powerbbsContent']")
                            .SelectNodesOrThrow(".//img")
                            .Select(img => img.GetSrc().Split("?")[0])
                            .ToStringImageLinkWrapperList();
        
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
