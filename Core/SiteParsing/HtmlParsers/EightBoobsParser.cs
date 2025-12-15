using Common.ExtensionMethods;
using Core.DataStructures;
using Core.Enums;
using Core.ExtensionMethods;
using Core.Managers;
using WebDriver = Core.Driver.WebDriver;

namespace Core.SiteParsing.HtmlParsers;

public class EightBoobsParser : HtmlParser, IHtmlParser
{
    public static string ParserName => "8boobs";

    public EightBoobsParser(WebDriver driver, ApiClientManager clientManager, Dictionary<string, string> requestHeaders, FilenameScheme filenameScheme = FilenameScheme.Original) : base(driver, clientManager, requestHeaders, IHtmlParser.GetFilenameScheme<EightBoobsParser>(filenameScheme))
    {
    }

    /// <summary>
    ///     Parses the html for 8boobs.com and extracts the relevant information necessary for downloading images from the site
    /// </summary>
    /// <returns>A RipInfo object containing the image links and the directory name</returns>
    protected override async Task<RipInfo> Parse()
    {
        var soup = await Soupify();
        var dirName = soup.SelectSingleNodeOrThrow("//div[@id='content']")
                            .SelectNodesOrThrow(".//div[@class='title']")[1]
                            .InnerText;
        var images = soup.SelectSingleNodeOrThrow("//div[@class='gallery clear']")
                            .SelectNodesOrThrow("./a")
                            .Select(img => Protocol + img
                                        .SelectSingleNodeOrThrow(".//img")
                                        .GetSrc()
                                        .Remove("tn_"))
                            .ToStringImageLinkWrapperList();
    
        return RipInfo.FromUrlList(images, dirName, FilenameScheme);
    }
}
