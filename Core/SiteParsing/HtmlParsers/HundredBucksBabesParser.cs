using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class HundredBucksBabesParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "100bucksbabes";

    public HundredBucksBabesParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, filenameScheme)
    {
    }

    /// <summary>
    ///     Parses the html for 100bucksbabes.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//div[@class='main-col-2']")
                            .SelectSingleNodeOrThrow(".//h2[@class='heading']")
                            .InnerText;
        var images = soup.SelectSingleNodeOrThrow("//div[@class='main-thumbs']")
                            .SelectNodesOrThrow(".//img")
                            .Select(img => Protocol + img.GetAttributeValue("data-url"))
                            .ToStringImageLinkWrapperList();
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
