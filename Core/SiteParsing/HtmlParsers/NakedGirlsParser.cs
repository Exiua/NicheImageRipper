using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class NakedGirlsParser : HtmlParser
{
    public NakedGirlsParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for nakedgirls.xxx and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//div[@class='content']")
                            .SelectSingleNodeOrThrow(".//h1")
                            .InnerText;
        var images = soup.SelectSingleNodeOrThrow("//div[@class='content']")
                            .SelectNodesOrThrow(".//div[@class='thumb']")
                            .Select(img => "https://www.nakedgirls.xxx" + img.SelectSingleNodeOrThrow(".//a").GetHref())
                            .ToStringImageLinkWrapperList();
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
